using BenchmarkDotNet.Attributes;
using Microsoft.SqlServer.Types;

namespace RxSpatial.SqlServer.Benchmarks;

/// <summary>
/// The planar counterpart of <see cref="SqlSpatialBackendBenchmarks"/>: the cost of one location
/// update pushed through <see cref="RxSqlGeometry"/> — spatial index lookup plus notification of
/// the intersection observers in the same zone — for every backend, on a 1,000 × 1,000 floor
/// (SRID 0) covered by the default <see cref="RxSpatialOptions.PlanarGrid"/>. The workload has
/// the same shape as the geographic one, so the two tables compare SQL Server's planar and
/// geodetic operations under the same engine.
/// </summary>
[MemoryDiagnoser]
public class SqlPlanarBackendBenchmarks
{
    [Params(
        RxGeographyDataStruct.List,
        RxGeographyDataStruct.HashMap,
        RxGeographyDataStruct.RTree,
        RxGeographyDataStruct.RumTree)]
    public RxGeographyDataStruct Backend;

    [Params(100, 1000)]
    public int Observers;

    private RxSqlGeometry vehicle = null!;
    private SqlGeometry[] route = null!;
    private IDisposable[] subscriptions = null!;
    private int step;

    [GlobalSetup]
    public void Setup()
    {
        vehicle = new RxSqlGeometry(new RxSpatialOptions
        {
            PreferredDataStruct = Backend,
            IsVisualized = true,
            RumTreeCleaningInterval = 5,
            PlanarGrid = new PlanarGridOptions
            {
                CoverageRegion = new PlanarCoverageRegionOptions { MinX = 0, MinY = 0, MaxX = 1000, MaxY = 1000 },
            },
        });

        // Stationary observation zones spread over the coverage region.
        Random random = new(42);
        subscriptions = new IDisposable[Observers];
        for (int i = 0; i < Observers; i++)
        {
            SqlGeometry zone = RandomPoint(random).STBuffer(20);
            subscriptions[i] = vehicle.RxIntersect(zone, new NullObserver<bool>());
        }

        // Position updates are small buffered regions rather than bare points, as in the
        // geographic benchmark (the tree backends index bounding boxes).
        route = new SqlGeometry[64];
        for (int i = 0; i < route.Length; i++)
        {
            route[i] = RandomPoint(random).STBuffer(0.5);
        }
    }

    [Benchmark]
    public void PushLocationUpdate()
    {
        vehicle.OnNext(route[step & 63]);
        step++;
    }

    private static SqlGeometry RandomPoint(Random random)
    {
        double x = random.NextDouble() * 1000;
        double y = random.NextDouble() * 1000;
        return SqlGeometry.Point(x, y, 0);
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
