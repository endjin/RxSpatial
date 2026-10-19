using BenchmarkDotNet.Attributes;
using Microsoft.SqlServer.Types;

namespace RxSpatial.SqlServer.Benchmarks;

/// <summary>
/// The SQL Server baseline of the cross-platform SpatialBackendBenchmarks, with the same
/// workload: the cost of one location update pushed through <see cref="RxSqlGeography"/> —
/// spatial index lookup plus notification of the intersection observers in the same zone —
/// for every backend. The push is processed
/// synchronously (<c>isVisualized=true</c>), so the measured time is the full processing
/// cost of an update rather than queueing latency. The 'list' backend evaluates every
/// observer on every update; the grid and tree backends prune by zone.
/// </summary>
[MemoryDiagnoser]
public class SqlSpatialBackendBenchmarks
{
    [Params(
        RxGeographyDataStruct.List,
        RxGeographyDataStruct.HashMap,
        RxGeographyDataStruct.RTree,
        RxGeographyDataStruct.RumTree)]
    public RxGeographyDataStruct Backend;

    [Params(100, 1000)]
    public int Observers;

    private RxSqlGeography vehicle = null!;
    private SqlGeography[] route = null!;
    private IDisposable[] subscriptions = null!;
    private int step;

    [GlobalSetup]
    public void Setup()
    {
        vehicle = new RxSqlGeography(new RxSpatialOptions
        {
            PreferredDataStruct = Backend,
            IsVisualized = true,
            RumTreeCleaningInterval = 5,
        });

        // Stationary observation zones spread over the coverage region, like the polygon
        // grid the original load test subscribed against.
        Random random = new(42);
        subscriptions = new IDisposable[Observers];
        for (int i = 0; i < Observers; i++)
        {
            SqlGeography zone = RandomPoint(random).STBuffer(2000);
            subscriptions[i] = vehicle.RxIntersect(zone, new NullObserver<bool>());
        }

        // Position updates are small buffered regions rather than bare points, matching the
        // original load-test traffic generator (the tree backends index bounding boxes).
        route = new SqlGeography[64];
        for (int i = 0; i < route.Length; i++)
        {
            route[i] = RandomPoint(random).STBuffer(50);
        }
    }

    [Benchmark]
    public void PushLocationUpdate()
    {
        vehicle.OnNext(route[step & 63]);
        step++;
    }

    private static SqlGeography RandomPoint(Random random)
    {
        double lat = 46 + (random.NextDouble() * 3);
        double lon = -124 + (random.NextDouble() * 4);
        return SqlGeography.Point(lat, lon, 4326);
    }

    private sealed class NullObserver<T> : IObserver<T>
    {
        public void OnNext(T value)
        {
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
