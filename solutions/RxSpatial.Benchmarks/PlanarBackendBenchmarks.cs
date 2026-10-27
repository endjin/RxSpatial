// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using BenchmarkDotNet.Attributes;
using RxSpatial.Geo;

namespace RxSpatial.Benchmarks;

/// <summary>
/// The planar counterpart of <see cref="SpatialBackendBenchmarks"/>: the cost of one location
/// update pushed through <see cref="RxGeometry"/> — spatial index lookup plus notification of
/// the intersection observers in the same zone — for every backend, on a 1,000 × 1,000 floor
/// covered by the default <see cref="RxSpatialOptions.PlanarGrid"/>. The workload has the same
/// shape as the geographic one, so the two tables compare the planar and geodetic shape maths
/// under the same engine. RxSpatial.SqlServer.Benchmarks runs the same workload on SQL Server.
/// </summary>
[MemoryDiagnoser]
public class PlanarBackendBenchmarks
{
    [Params(
        RxGeographyDataStruct.List,
        RxGeographyDataStruct.HashMap,
        RxGeographyDataStruct.RTree,
        RxGeographyDataStruct.RumTree)]
    public RxGeographyDataStruct Backend;

    [Params(100, 1000)]
    public int Observers;

    private RxGeometry vehicle = null!;
    private PlanarShape[] route = null!;
    private IDisposable[] subscriptions = null!;
    private int step;

    [GlobalSetup]
    public void Setup()
    {
        vehicle = new RxGeometry(new RxSpatialOptions
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
            PlanarCircle zone = new(RandomPoint(random), 20);
            subscriptions[i] = vehicle.RxIntersect(zone, new NullObserver<bool>());
        }

        // Position updates are small buffered regions rather than bare points, as in the
        // geographic benchmark (the tree backends index bounding boxes).
        route = new PlanarShape[64];
        for (int i = 0; i < route.Length; i++)
        {
            route[i] = new PlanarCircle(RandomPoint(random), 0.5);
        }
    }

    [Benchmark]
    public void PushLocationUpdate()
    {
        vehicle.OnNext(route[step & 63]);
        step++;
    }

    private static PlanarPoint RandomPoint(Random random)
    {
        double x = random.NextDouble() * 1000;
        double y = random.NextDouble() * 1000;
        return new PlanarPoint(x, y);
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
