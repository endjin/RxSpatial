using System.Diagnostics;
using RxSpatial.Benchmarks.Shared;
using RxSpatial.Geo;

namespace RxSpatial.Experiments;

/// <summary>
/// Not a paper figure: the cost of processing one location update with nothing queued,
/// measured synchronously (visualised mode) for one moving object with the 2000 geofences of
/// Fig. 6. It adds a fifth row the paper did not have, <c>NaiveLoop</c>: a plain loop of
/// <see cref="GeoOperations.Intersects"/> over the same fences with no RxSpatial at all, to check that the List
/// variant is a faithful stand-in for "every operation from scratch". The service time
/// explains the other figures: latency is service time plus queueing, and a system of n
/// sources at inter-arrival T saturates near cores × T ÷ service time.
/// </summary>
internal static class ServiceTimeExperiment
{
    public const string NaiveLoop = "NaiveLoop";

    public static Dictionary<string, double> Run(ExperimentContext context)
    {
        int warmup = 20;
        int updates = context.Scale(200, 2000);
        GeoShape[] route = context.Workload.Route(0, warmup + updates);
        IReadOnlyList<GeoCircle> fences = context.Workload.Fences;
        List<IReadOnlyList<object>> rows = [];
        Dictionary<string, double> meanMicroseconds = [];
        Dictionary<string, long> matches = [];

        ExperimentContext.Log(FormattableString.Invariant($"service: {updates} synchronous updates, {fences.Count} fences"));
        foreach (Variant variant in ExperimentContext.Variants)
        {
            using Fleet fleet = new(variant, context.Workload, fences.Count, ExperimentContext.InputBuffer, route.Length, visualized: true);
            TrackedObject tracked = fleet.Take(1)[0];
            double[] times = Measure(warmup, updates, () => tracked.TryPush());
            long evaluations = tracked.Evaluations;
            matches[variant.Label] = tracked.Matches;
            rows.Add(Row(variant.Label, times, (double)evaluations / (warmup + updates), fleet.BuildTime.TotalMilliseconds));
            meanMicroseconds[variant.Label] = times.Average();
        }

        int cursor = 0;
        long naiveMatches = 0;
        double[] naive = Measure(warmup, updates, () =>
        {
            GeoShape location = route[cursor++];
            foreach (GeoCircle fence in fences)
            {
                if (GeoOperations.Intersects(fence, location))
                    naiveMatches++;
            }
        });
        matches[NaiveLoop] = naiveMatches;
        rows.Add(Row(NaiveLoop, naive, fences.Count, 0));
        meanMicroseconds[NaiveLoop] = naive.Average();

        string[] header = ["variant", "mean_us", "median_us", "p99_us", "evaluations_per_update", "build_ms"];
        context.Report.WriteCsv("service", header, rows);

        bool consistent = matches.Values.Distinct().Count() == 1;
        double list = meanMicroseconds[Variant.SqlSpatial.Label];
        context.Report.Section(FormattableString.Invariant($"""
            ## Service time per location update (not a paper figure)

            One moving object, {fences.Count} `RxIntersect` geofences, {updates} updates processed synchronously (visualised mode) after {warmup} warm-up updates. `NaiveLoop` is a plain `GeoOperations.Intersects` loop over the same fences with no RxSpatial.

            {Report.Table(["Variant", "Mean (µs)", "Median (µs)", "p99 (µs)", "Observers evaluated / update", "Subscribe time (ms)"], rows)}
            List (SqlSpatial) ÷ NaiveLoop mean: **{list / meanMicroseconds[NaiveLoop]:F2}×**. Matches per variant: {string.Join(", ", matches.Select(m => FormattableString.Invariant($"{m.Key} {m.Value}")))} — {(consistent ? "identical, the indexes prune without losing answers" : "**MISMATCH**")}.
            """));

        List<IReadOnlyList<object>> scaling = Scaling(context, route, fences);
        context.Report.WriteCsv("service-scaling", ["threads", "fence_instances", "intersects_per_second", "speedup", "busy_cores"], scaling);
        context.Report.Section($"""
            ### Parallel scaling of `GeoOperations.Intersects`

            Each thread loops `GeoOperations.Intersects` of its own route points against the fences for 2 s; "shared" threads use the same `GeoCircle` instances (as every moving object in the experiments does), "copies" give each thread its own copies. This bounds every multi-object experiment: the drainers of many moving objects run in parallel only as far as the shape maths does. "Busy cores" is the process's CPU time over the wall-clock time, and the last row repeats the single-thread run: if it has drifted from the first, the machine slowed down during the table (power or thermal throttling) and the speed-ups are not comparable. Windows power throttling: {PowerThrottling.Describe()} — a throttled background process on a hybrid laptop slowed ~6.7× a few seconds into the run, which once made this table read ~1× at every thread count.

            {Report.Table(["Threads", "Fence instances", "Intersects / s", "Speed-up vs 1 thread", "Busy cores"], scaling)}
            """);
        return meanMicroseconds;
    }

    private static List<IReadOnlyList<object>> Scaling(ExperimentContext context, GeoShape[] route, IReadOnlyList<GeoCircle> fences)
    {
        int processors = Environment.ProcessorCount;
        int[] threadCounts = [.. new[] { 1, 2, 4, 8, processors }.Where(t => t <= processors).Distinct()];
        ExperimentContext.Log(FormattableString.Invariant($"service: Intersects scaling over {string.Join('/', threadCounts)} threads"));
        GeoCircle[] shared = [.. fences];
        List<IReadOnlyList<object>> rows = [];
        double single = 0;
        foreach (int threads in threadCounts)
            Add(threads.ToString(System.Globalization.CultureInfo.InvariantCulture), "shared", threads, _ => shared);

        Add(FormattableString.Invariant($"{processors}"), "copies", processors, _ =>
            [.. fences.Select(f => new GeoCircle(f.Center, f.RadiusMetres))]);
        Add("1 (again)", "shared", 1, _ => shared);
        return rows;

        void Add(string label, string kind, int threads, Func<int, GeoCircle[]> fencesFor)
        {
            (double rate, double busyCores) = Throughput(threads, route, fencesFor);
            if (single == 0)
                single = rate;

            rows.Add([label, kind, rate, rate / single, busyCores]);
        }
    }

    // Aggregate Intersects calls per second of `threads` threads looping for two seconds, and
    // how many cores the process kept busy meanwhile.
    private static (double Rate, double BusyCores) Throughput(int threads, GeoShape[] route, Func<int, GeoCircle[]> fencesFor)
    {
        TimeSpan duration = TimeSpan.FromSeconds(2);
        long total = 0;
        using Barrier barrier = new(threads);
        Thread[] workers = [.. Enumerable.Range(0, threads).Select(t => new Thread(() => Work(t)))];
        using Process process = Process.GetCurrentProcess();
        TimeSpan cpuBefore = process.TotalProcessorTime;
        long wallBefore = Stopwatch.GetTimestamp();
        foreach (Thread worker in workers)
            worker.Start();

        foreach (Thread worker in workers)
            worker.Join();

        double wall = Stopwatch.GetElapsedTime(wallBefore).TotalSeconds;
        process.Refresh();
        double busyCores = (process.TotalProcessorTime - cpuBefore).TotalSeconds / wall;
        return (total / duration.TotalSeconds, busyCores);

        void Work(int t)
        {
            GeoCircle[] own = fencesFor(t);
            long count = 0;
            barrier.SignalAndWait();
            long start = Stopwatch.GetTimestamp();
            for (int i = t; Stopwatch.GetElapsedTime(start) < duration; i++)
            {
                GeoShape location = route[i % route.Length];
                foreach (GeoCircle fence in own)
                    _ = GeoOperations.Intersects(fence, location);

                count += own.Length;
            }

            Interlocked.Add(ref total, count);
        }
    }

    private static IReadOnlyList<object> Row(string name, double[] times, double evaluationsPerUpdate, double buildMs)
    {
        double[] sorted = [.. times.Order()];
        return [name, times.Average(), sorted[sorted.Length / 2], sorted[(int)(sorted.Length * 0.99)], evaluationsPerUpdate, buildMs];
    }

    // Per-update times in microseconds, after untimed warm-up calls.
    private static double[] Measure(int warmup, int updates, Action update)
    {
        for (int i = 0; i < warmup; i++)
            update();

        double[] times = new double[updates];
        for (int i = 0; i < updates; i++)
        {
            long start = Stopwatch.GetTimestamp();
            update();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
        }

        return times;
    }
}
