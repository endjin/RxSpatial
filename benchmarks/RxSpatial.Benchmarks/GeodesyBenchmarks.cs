using BenchmarkDotNet.Attributes;
using RxSpatial.Geo;

namespace RxSpatial.Benchmarks;

/// <summary>
/// The geodesic primitives under every <see cref="GeoOperations"/> call, one at a time:
/// Vincenty's inverse (with and without the final azimuth) and direct solutions at city
/// (1.4 km) and regional (230 km, Seattle to Portland) range, the approximate
/// <see cref="Wgs84.FastDistance"/> that guards point-in-circle tests, and the
/// <see cref="LocalPlane"/> projection both ways. None of them allocates.
/// </summary>
[MemoryDiagnoser]
public class GeodesyBenchmarks
{
    private readonly GeoPoint needle = new(47.6205, -122.3493);
    private readonly GeoPoint route = new(47.6300, -122.3550);
    private readonly GeoPoint portland = new(45.5152, -122.6784);

    private readonly LocalPlane plane;
    private readonly PlanarPoint projected;

    public GeodesyBenchmarks()
    {
        plane = new(needle);
        projected = plane.Project(route);
    }

    [Benchmark]
    public GeodesicInverse Inverse() => Wgs84.Inverse(needle, route);

    [Benchmark]
    public double InverseDistanceOnly() => Wgs84.Distance(needle, route);

    [Benchmark]
    public double InverseDistanceOnly_Regional() => Wgs84.Distance(needle, portland);

    [Benchmark]
    public GeoPoint Direct() => Wgs84.Direct(needle, 63, 1_500);

    [Benchmark]
    public GeoPoint Direct_Regional() => Wgs84.Direct(needle, 190, 230_000);

    [Benchmark]
    public double FastDistance() => Wgs84.FastDistance(needle, route);

    [Benchmark]
    public PlanarPoint Project() => plane.Project(route);

    [Benchmark]
    public GeoPoint Unproject() => plane.Unproject(projected);
}
