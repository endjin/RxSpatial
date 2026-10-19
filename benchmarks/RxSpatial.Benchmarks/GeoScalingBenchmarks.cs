using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using RxSpatial.Geo;

namespace RxSpatial.Benchmarks;

/// <summary>
/// Multi-core throughput of the shape maths alone: many moving objects checked against shared
/// geofences, as the buffered drainers of a fleet do, without RxSpatial, Rx or the indexes in
/// the way. <see cref="Threads"/> dedicated worker threads, started once per parameter value,
/// pull one route point at a time (that point against every fence) from a shared counter, so
/// that faster performance cores take more of the work than efficiency cores instead of
/// waiting for them. Each worker keeps its tallies in locals and publishes them once, into its
/// own cache-line-padded slot; the counter has a line to itself. The fences and route are
/// shared and never written, so nothing but the counter crosses cores.
/// </summary>
/// <remarks>
/// Times are per operation (one Intersects or Distance), aggregated over all threads; the
/// <c>Mops/s</c> column is the aggregate rate and <c>Scaling</c> the speed-up over one thread.
/// <list type="bullet">
/// <item><see cref="IntersectsFences"/>: a point against 2,000 exact geodesic circles of
/// 100–400 m over the Seattle area (the paper's §VI-A geofence workload, mostly decided by the
/// bounds check).</item>
/// <item><see cref="MixedDistanceIntersects"/>: a point against 1,500 circles and 500
/// rectangles, alternating Intersects and Distance (Vincenty per circle, projection per
/// rectangle) — the maths-heavy end.</item>
/// </list>
/// Speed-ups beyond the 6 performance cores (12 threads) of the reference machine come from
/// efficiency cores and hyper-threads, so they are sub-linear by design of the hardware.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(Columns))]
public class GeoScalingBenchmarks : IDisposable
{
    private const int Fences = 2_000;
    private const int IntersectsRoute = 480;
    private const int MixedRoute = 192;

    private GeoShape[] circles = null!;
    private GeoShape[] mixed = null!;
    private GeoShape[] route = null!;
    private Worker[] workers = null!;
    private Barrier barrier = null!;
    private PaddedLong next;
    private Func<GeoShape, GeoShape[], double> operation = null!;
    private GeoShape[] fences = null!;
    private int routeLength;
    private PaddedDouble[] results = null!;

    /// <summary>The worker threads.</summary>
    [Params(1, 2, 4, 8, 14, 20)]
    public int Threads;

    [GlobalSetup]
    public void Setup()
    {
        Random random = new(42);
        circles = new GeoShape[Fences];
        mixed = new GeoShape[Fences];
        for (int i = 0; i < Fences; i++)
        {
            GeoPoint centre = RandomPoint(random);
            circles[i] = new GeoCircle(centre, 100 + (random.NextDouble() * 300));
            mixed[i] = i % 4 == 3
                ? GeoPolygon.Rectangle(centre.Latitude, centre.Longitude, centre.Latitude + 0.002 + (random.NextDouble() * 0.005), centre.Longitude + 0.003 + (random.NextDouble() * 0.008))
                : new GeoCircle(centre, 100 + (random.NextDouble() * 300));
        }

        route = new GeoShape[IntersectsRoute];
        for (int i = 0; i < route.Length; i++)
            route[i] = new GeoPointShape(RandomPoint(random));

        results = new PaddedDouble[Threads];
        barrier = new Barrier(Threads + 1);
        workers = new Worker[Threads];
        for (int t = 0; t < Threads; t++)
            workers[t] = new Worker(this, t);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        operation = null!;
        barrier.SignalAndWait(); // releases the workers, which see no operation and exit
        foreach (Worker worker in workers)
            worker.Join();

        Dispose();
    }

    public void Dispose()
    {
        barrier?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(OperationsPerInvoke = IntersectsRoute * Fences)]
    public double IntersectsFences() => Run(circles, IntersectsRoute, static (point, fences) =>
    {
        double matches = 0;
        foreach (GeoShape fence in fences)
        {
            if (GeoOperations.Intersects(fence, point))
                matches++;
        }

        return matches;
    });

    [Benchmark(OperationsPerInvoke = MixedRoute * Fences)]
    public double MixedDistanceIntersects() => Run(mixed, MixedRoute, static (point, fences) =>
    {
        double sum = 0;
        for (int i = 0; i < fences.Length; i += 2)
        {
            sum += GeoOperations.Intersects(fences[i], point) ? 1 : 0;
            sum += GeoOperations.Distance(fences[i + 1], point);
        }

        return sum;
    });

    private double Run(GeoShape[] fenceSet, int length, Func<GeoShape, GeoShape[], double> work)
    {
        fences = fenceSet;
        routeLength = length;
        operation = work;
        next.Value = -1;
        barrier.SignalAndWait(); // start
        barrier.SignalAndWait(); // finish
        double total = 0;
        foreach (PaddedDouble result in results)
            total += result.Value;

        return total;
    }

    private void Work(int index)
    {
        while (true)
        {
            barrier.SignalAndWait();
            Func<GeoShape, GeoShape[], double>? work = operation;
            if (work is null)
                return;

            GeoShape[] shared = fences;
            double sum = 0;
            for (long i = Interlocked.Increment(ref next.Value); i < routeLength; i = Interlocked.Increment(ref next.Value))
                sum += work(route[i], shared);

            results[index].Value = sum;
            barrier.SignalAndWait();
        }
    }

    private static GeoPoint RandomPoint(Random random)
        => new(47.50 + (random.NextDouble() * 0.25), -122.45 + (random.NextDouble() * 0.25));

    /// <summary>A value alone on its 64-byte cache line (and its neighbour, against the adjacent-line prefetcher).</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct PaddedLong
    {
        [FieldOffset(64)]
        public long Value;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct PaddedDouble
    {
        [FieldOffset(64)]
        public double Value;
    }

    private sealed class Worker
    {
        private readonly Thread thread;

        public Worker(GeoScalingBenchmarks owner, int index)
        {
            thread = new Thread(() => owner.Work(index)) { IsBackground = true, Name = $"Geo worker {index}" };
            thread.Start();
        }

        public void Join() => thread.Join();
    }

    private sealed class Columns : ManualConfig
    {
        public Columns() => AddColumn(new RateColumn(), new ScalingColumn());
    }

    /// <summary>Aggregate millions of operations per second, from the mean time per operation.</summary>
    private sealed class RateColumn : IColumn
    {
        public string Id => nameof(RateColumn);

        public string ColumnName => "Mops/s";

        public bool AlwaysShow => true;

        public ColumnCategory Category => ColumnCategory.Custom;

        public int PriorityInCategory => 0;

        public bool IsNumeric => true;

        public UnitType UnitType => UnitType.Dimensionless;

        public string Legend => "Aggregate million operations per second over all threads";

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
            => summary[benchmarkCase]?.ResultStatistics is { } stats
                ? (1_000 / stats.Mean).ToString("N1", System.Globalization.CultureInfo.InvariantCulture)
                : "-";

        public bool IsAvailable(Summary summary) => true;

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    }

    /// <summary>The speed-up over the same benchmark with one thread.</summary>
    private sealed class ScalingColumn : IColumn
    {
        public string Id => nameof(ScalingColumn);

        public string ColumnName => "Scaling";

        public bool AlwaysShow => true;

        public ColumnCategory Category => ColumnCategory.Custom;

        public int PriorityInCategory => 1;

        public bool IsNumeric => true;

        public UnitType UnitType => UnitType.Dimensionless;

        public string Legend => "Aggregate throughput relative to Threads=1";

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            BenchmarkCase? single = summary.BenchmarksCases.FirstOrDefault(c =>
                c.Descriptor == benchmarkCase.Descriptor && c.Job.DisplayInfo == benchmarkCase.Job.DisplayInfo && Equals(c.Parameters[nameof(Threads)], 1));
            return single is not null
                && summary[single]?.ResultStatistics is { } baseline
                && summary[benchmarkCase]?.ResultStatistics is { } stats
                ? (baseline.Mean / stats.Mean).ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "×"
                : "-";
        }

        public bool IsAvailable(Summary summary) => true;

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
    }
}
