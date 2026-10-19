using BenchmarkDotNet.Attributes;
using RxSpatial.Geo;

namespace RxSpatial.Benchmarks;

/// <summary>
/// What building a polygon costs: from corners, from a vertex list and from WKT, for a
/// 4-vertex zone and a 64-vertex ring (a 2 km disc's outline). A geodesic polygon projects its
/// vertices into a local plane, validates the ring (area, convexity, single winding) and
/// computes its bounds and edge lengths; <c>ValidateConvexity</c> isolates the validation, and
/// the planar polygon (no projection) shows the rest. Fences are built once and queried many
/// times, so this is set-up cost, but the demo builds a polygon on every drawn zone.
/// </summary>
[MemoryDiagnoser]
public class ShapeConstructionBenchmarks
{
    private static readonly GeoPoint Needle = new(47.6205, -122.3493);

    private readonly GeoPoint[] corners =
        [new(47.600, -122.370), new(47.600, -122.330), new(47.618, -122.330), new(47.618, -122.370)];

    private readonly GeoPoint[] ring64 = GeoOperations.ToVertices(new GeoCircle(Needle, 2_000), 64);
    private readonly PlanarPoint[] planarCorners = [new(450, 450), new(550, 450), new(550, 550), new(450, 550)];
    private readonly PlanarPoint[] planarRing64 = PlanarOperations.ToVertices(new PlanarCircle(new(500, 500), 50), 64);

    private string wkt4 = null!;
    private string wkt64 = null!;

    [GlobalSetup]
    public void Setup()
    {
        wkt4 = GeoPolygon.FromVertices(corners).ToString();
        wkt64 = GeoPolygon.FromVertices(ring64).ToString();
    }

    [Benchmark]
    public GeoPolygon Rectangle() => GeoPolygon.Rectangle(corners[0].Latitude, corners[0].Longitude, corners[2].Latitude, corners[2].Longitude);

    [Benchmark]
    public GeoPolygon FromVertices_4() => GeoPolygon.FromVertices(corners);

    [Benchmark]
    public GeoPolygon FromVertices_64() => GeoPolygon.FromVertices(ring64);

    [Benchmark]
    public GeoPolygon FromWkt_4() => GeoPolygon.FromWkt(wkt4);

    [Benchmark]
    public GeoPolygon FromWkt_64() => GeoPolygon.FromWkt(wkt64);

    [Benchmark]
    public bool ValidateConvexity_4() => ConvexMath.OrientCounterClockwise(planarCorners, null);

    [Benchmark]
    public bool ValidateConvexity_64() => ConvexMath.OrientCounterClockwise(planarRing64, null);

    [Benchmark]
    public PlanarPolygon PlanarFromVertices_4() => PlanarPolygon.FromVertices(planarCorners);

    [Benchmark]
    public PlanarPolygon PlanarFromVertices_64() => PlanarPolygon.FromVertices(planarRing64);
}
