using System.Diagnostics;
using Microsoft.SqlServer.Types;

namespace RxSpatial.Experiments;

/// <summary>
/// Not a paper figure: the cost of processing one location update with nothing queued,
/// measured synchronously (visualised mode) for one moving object with the 2000 geofences of
/// Fig. 6. It adds a fifth row the paper did not have, <c>NaiveLoop</c>: a plain loop of
/// <c>STIntersects</c> over the same fences with no RxSpatial at all, to check that the List
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
        SqlGeography[] route = context.Workload.Route(0, warmup + updates);
        IReadOnlyList<SqlGeography> fences = context.Workload.Fences;
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
            SqlGeography location = route[cursor++];
            foreach (SqlGeography fence in fences)
            {
                if (fence.STIntersects(location).IsTrue)
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

            One moving object, {fences.Count} `RxIntersect` geofences, {updates} updates processed synchronously (visualised mode) after {warmup} warm-up updates. `NaiveLoop` is a plain `STIntersects` loop over the same fences with no RxSpatial.

            {Report.Table(["Variant", "Mean (µs)", "Median (µs)", "p99 (µs)", "Observers evaluated / update", "Subscribe time (ms)"], rows)}
            List (SqlSpatial) ÷ NaiveLoop mean: **{list / meanMicroseconds[NaiveLoop]:F2}×**. Matches per variant: {string.Join(", ", matches.Select(m => FormattableString.Invariant($"{m.Key} {m.Value}")))} — {(consistent ? "identical, the indexes prune without losing answers" : "**MISMATCH**")}.
            """));

        List<IReadOnlyList<object>> scaling = Scaling(context, route, fences);
        context.Report.WriteCsv("service-scaling", ["threads", "fence_instances", "stintersects_per_second", "speedup"], scaling);
        context.Report.Section($"""
            ### Parallel scaling of `STIntersects`

            Each thread loops `STIntersects` of its own route points against the fences for 2 s; "shared" threads use the same `SqlGeography` instances (as every moving object in the experiments does), "copies" give each thread its own deserialised copies. This bounds every multi-object experiment: the drainers of many moving objects run in parallel only as far as SQL Server's spatial library does.

            {Report.Table(["Threads", "Fence instances", "STIntersects / s", "Speed-up vs 1 thread"], scaling)}
            """);
        return meanMicroseconds;
    }

    private static List<IReadOnlyList<object>> Scaling(ExperimentContext context, SqlGeography[] route, IReadOnlyList<SqlGeography> fences)
    {
        int processors = Environment.ProcessorCount;
        int[] threadCounts = [.. new[] { 1, 2, 4, 8, processors }.Where(t => t <= processors).Distinct()];
        ExperimentContext.Log(FormattableString.Invariant($"service: STIntersects scaling over {string.Join('/', threadCounts)} threads"));
        List<IReadOnlyList<object>> rows = [];
        double single = 0;
        foreach (int threads in threadCounts)
            Add(threads.ToString(System.Globalization.CultureInfo.InvariantCulture), "shared", threads, _ => fences);

        Add(FormattableString.Invariant($"{processors}"), "copies", processors, _ =>
            [.. fences.Select(f => SqlGeography.STGeomFromWKB(f.STAsBinary(), f.STSrid.Value))]);
        return rows;

        void Add(string label, string kind, int threads, Func<int, IReadOnlyList<SqlGeography>> fencesFor)
        {
            double rate = Throughput(threads, route, fencesFor, context.Workload);
            if (single == 0)
                single = rate;

            rows.Add([label, kind, rate, rate / single]);
        }
    }

    // Aggregate STIntersects per second of `threads` threads looping for two seconds.
    private static double Throughput(int threads, SqlGeography[] route, Func<int, IReadOnlyList<SqlGeography>> fencesFor, SeattleWorkload workload)
    {
        TimeSpan duration = TimeSpan.FromSeconds(2);
        long total = 0;
        using Barrier barrier = new(threads);
        Thread[] workers = [.. Enumerable.Range(0, threads).Select(t => new Thread(() => Work(t)))];
        foreach (Thread worker in workers)
            worker.Start();

        foreach (Thread worker in workers)
            worker.Join();

        return total / duration.TotalSeconds;

        void Work(int t)
        {
            IReadOnlyList<SqlGeography> own = fencesFor(t);
            SqlGeography[] points = t == 0 ? route : workload.Route(t, route.Length);
            long count = 0;
            barrier.SignalAndWait();
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; Stopwatch.GetElapsedTime(start) < duration; i++)
            {
                SqlGeography location = points[i % points.Length];
                foreach (SqlGeography fence in own)
                    _ = fence.STIntersects(location);

                count += own.Count;
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
