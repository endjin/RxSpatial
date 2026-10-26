// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using Shouldly;

namespace RxSpatial.Tests.Geo;

/// <summary>
/// The hot-path operations (Intersects, Distance, Area, Bounds) allocate nothing: they run
/// on every location update. Measured with the current thread's allocation counter after a
/// warm-up, so that tiered compilation and pooled buffers are already in place.
/// </summary>
[TestClass]
public class AllocationTests
{
    private static readonly GeoPoint Needle = new(47.6205, -122.3493);

    public static IEnumerable<object[]> GeoPairs()
    {
        GeoPointShape point = new(47.6300, -122.3550);
        GeoCircle fence = new(Needle, 500);
        GeoCircle reach = new(new(47.6250, -122.3400), 2_000);
        GeoPolygon zone = GeoPolygon.Rectangle(47.600, -122.370, 47.618, -122.330);
        GeoPolygon lens = (GeoPolygon)GeoOperations.Intersection(new GeoCircle(Needle, 2_000), reach); // > 128 vertices: pooled path
        GeoShape[] shapes = [point, fence, reach, zone, lens, GeoEmpty.Instance];
        foreach (GeoShape a in shapes)
        {
            foreach (GeoShape b in shapes)
                yield return [a, b];
        }
    }

    public static IEnumerable<object[]> PlanarPairs()
    {
        PlanarShape[] shapes =
        [
            new PlanarPointShape(300, 500), new PlanarCircle(new(500, 500), 50),
            PlanarPolygon.Rectangle(450, 450, 550, 550), PlanarEmpty.Instance,
        ];
        foreach (PlanarShape a in shapes)
        {
            foreach (PlanarShape b in shapes)
                yield return [a, b];
        }
    }

    [TestMethod]
    [DynamicData(nameof(GeoPairs))]
    public void GeoHotPaths_AllocateNothing(GeoShape a, GeoShape b)
    {
        double sink = 0;
        void Run()
        {
            for (int i = 0; i < 100; i++)
            {
                sink += GeoOperations.Intersects(a, b) ? 1 : 0;
                sink += GeoOperations.Distance(a, b);
                sink += GeoOperations.Area(a);
                if (!a.IsEmpty)
                    sink += GeoOperations.Bounds(a).MaxLatitude;
            }
        }

        Run();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0, $"{a} / {b} (sink {sink})");
    }

    [TestMethod]
    public void ToVertices_IntoSpan_AllocatesNothing()
    {
        GeoCircle reach = new(Needle, 2_000);
        GeoPolygon zone = GeoPolygon.Rectangle(47.600, -122.370, 47.618, -122.330);
        PlanarCircle pallet = new(new(500, 500), 50);
        GeoPoint[] outline = new GeoPoint[128];
        PlanarPoint[] planarOutline = new PlanarPoint[128];
        int sink = 0;
        void Run()
        {
            for (int i = 0; i < 20; i++)
            {
                sink += GeoOperations.ToVertices(reach, outline, 64);
                sink += GeoOperations.ToVertices(zone, outline);
                sink += PlanarOperations.ToVertices(pallet, planarOutline);
            }
        }

        Run();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0, $"sink {sink}");
    }

    /// <summary>
    /// Intersection allocates its result (the polygon and its vertex and edge arrays) and nothing
    /// else: the working rings are per-thread scratch lists and projections are stack buffers.
    /// </summary>
    [TestMethod]
    public void Intersection_AllocatesOnlyItsResult()
    {
        GeoCircle reach = new(new(47.6100, -122.3500), 1_500);
        GeoCircle other = new(new(47.6150, -122.3400), 1_500);
        GeoPolygon zone = GeoPolygon.Rectangle(47.600, -122.370, 47.618, -122.330);
        GeoPolygon overlapping = GeoPolygon.Rectangle(47.610, -122.350, 47.630, -122.320);
        (GeoShape A, GeoShape B)[] pairs = [(reach, other), (reach, zone), (zone, overlapping)];
        foreach ((GeoShape a, GeoShape b) in pairs)
        {
            GeoOperations.Intersection(a, b);
            long before = GC.GetAllocatedBytesForCurrentThread();
            GeoPolygon result = GeoOperations.Intersection(a, b).ShouldBeOfType<GeoPolygon>();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // The result: two 16-byte vertex arrays and an 8-byte edge-length array, plus the polygon
            // object, its array headers and its plane (a working ring alone would be 2 KB more).
            long resultBytes = (result.VertexSpan.Length * 40L) + 512;
            allocated.ShouldBeLessThanOrEqualTo(resultBytes, $"{a} / {b}");
        }
    }

    /// <summary>The scratch rings never leak into a result: earlier results and other threads' results stay intact.</summary>
    [TestMethod]
    public void Intersection_ResultsAreIndependentOfLaterCallsAndThreads()
    {
        GeoCircle reach = new(new(47.6100, -122.3500), 1_500);
        GeoPolygon zone = GeoPolygon.Rectangle(47.600, -122.370, 47.618, -122.330);
        GeoPolygon lens = (GeoPolygon)GeoOperations.Intersection(reach, new GeoCircle(new(47.6150, -122.3400), 1_500));
        GeoPoint[] lensVertices = lens.VertexSpan.ToArray();
        GeoOperations.Intersection(reach, zone);
        GeoOperations.Intersection(zone, GeoPolygon.Rectangle(47.610, -122.350, 47.630, -122.320));
        lens.VertexSpan.ToArray().ShouldBe(lensVertices);

        GeoCircle[] discs = [.. Enumerable.Range(0, 64).Select(i => new GeoCircle(new(47.600 + (i * 0.0005), -122.36 + (i * 0.0004)), 800 + (i * 10)))];
        GeoPoint[][] sequential = [.. discs.Select(d => GeoOperations.ToVertices(GeoOperations.Intersection(d, zone)))];
        GeoPoint[][] parallel = new GeoPoint[discs.Length][];
        Parallel.For(0, discs.Length, i => parallel[i] = GeoOperations.ToVertices(GeoOperations.Intersection(discs[i], zone)));
        for (int i = 0; i < discs.Length; i++)
            parallel[i].ShouldBe(sequential[i]);
    }

    [TestMethod]
    [DynamicData(nameof(PlanarPairs))]
    public void PlanarHotPaths_AllocateNothing(PlanarShape a, PlanarShape b)
    {
        double sink = 0;
        void Run()
        {
            for (int i = 0; i < 100; i++)
            {
                sink += PlanarOperations.Intersects(a, b) ? 1 : 0;
                sink += PlanarOperations.Distance(a, b);
                sink += PlanarOperations.Area(a);
                if (!a.IsEmpty)
                    sink += PlanarOperations.Bounds(a).MaxX;
            }
        }

        Run();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0, $"{a} / {b} (sink {sink})");
    }
}
