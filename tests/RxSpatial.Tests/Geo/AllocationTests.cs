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
