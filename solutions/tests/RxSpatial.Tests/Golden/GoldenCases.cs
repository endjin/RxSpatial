// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using GeographicLib;
using static System.FormattableString;
using static RxSpatial.Tests.Golden.GoldenShape;

namespace RxSpatial.Tests.Golden;

/// <summary>
/// The inputs of the golden cases, without results. Geodetic inputs are the Seattle-scale
/// set of the cross-platform research: points placed with GeographicLib's direct solution
/// at known geodesic distances and azimuths from the Space Needle, 500 m and 2 km discs,
/// and the test zone rectangles. Planar inputs are the warehouse-floor shapes the tests and
/// examples use.
/// </summary>
internal static class GoldenCases
{
    public const double NeedleLat = 47.6205;
    public const double NeedleLon = -122.3493;

    // The ~3 km x 2 km test zone south of the Space Needle.
    private const double ZoneSouth = 47.600;
    private const double ZoneNorth = 47.618;
    private const double ZoneWest = -122.370;
    private const double ZoneEast = -122.330;

    /// <summary>A case before recording: an id, a category, its shapes and the exact clearance.</summary>
    public sealed record Input(string Id, string Category, GoldenShape A, GoldenShape? B = null, double? Clearance = null);

    public static IEnumerable<Input> Geodetic()
    {
        foreach (double s in (double[])[10, 100, 1_000, 5_000, 10_000, 50_000])
        {
            foreach (double azimuth in (double[])[0, 37, 90, 135, 225])
            {
                (double lat, double lon) = Direct(NeedleLat, NeedleLon, azimuth, s);
                yield return new(Invariant($"point-point/{s}m/az{azimuth}"), "point-point", GeoPoint(NeedleLat, NeedleLon), GeoPoint(lat, lon));
            }
        }

        foreach (double radius in (double[])[50, 100, 500, 2_000, 5_000])
            yield return new(Invariant($"circle-area/{radius}m"), "circle-area", GeoCircle(NeedleLat, NeedleLon, radius));

        yield return new("polygon-area/zone", "polygon-area", Zone());
        foreach ((string name, GoldenShape rectangle) in OtherRectangles())
            yield return new(Invariant($"polygon-area/{name}"), "polygon-area", rectangle);

        // The Examples' route against the 500 m Space Needle fence.
        (double Lat, double Lon)[] route =
        [
            (47.7000, -122.4000), (47.6600, -122.3700), (47.6300, -122.3550), (47.6205, -122.3493),
            (47.6250, -122.3493), (47.6205, -122.3560), (47.6200, -122.3600),
        ];
        foreach ((double lat, double lon) in route)
        {
            yield return new(
                Invariant($"point-circle/route/{lat},{lon}"),
                "point-circle",
                GeoCircle(NeedleLat, NeedleLon, 500),
                GeoPoint(lat, lon),
                Inverse(NeedleLat, NeedleLon, lat, lon) - 500);
        }

        // Points at r + delta between the eight principal directions, where a buffer's chords sag most.
        foreach (double delta in (double[])[-5, -2, -1, -0.5, -0.25, -0.1, 0.1, 0.25, 0.5, 1, 2, 5])
        {
            for (int k = 0; k < 8; k++)
            {
                double azimuth = 22.5 + (45 * k);
                (double lat, double lon) = Direct(NeedleLat, NeedleLon, azimuth, 500 + delta);
                yield return new(
                    Invariant($"point-circle/boundary/{delta}m/az{azimuth}"),
                    "point-circle",
                    GeoCircle(NeedleLat, NeedleLon, 500),
                    GeoPoint(lat, lon),
                    delta);
            }
        }

        // 2 km lenses (the demo's reach) and disc-disc distances.
        foreach (double separation in (double[])[500, 1_000, 2_000, 3_000, 3_900, 3_999, 4_001, 4_100])
            yield return DiscPair("circle-circle/lens", separation, 63);
        foreach (double separation in (double[])[4_500, 6_000, 10_000])
            yield return DiscPair("circle-circle/apart", separation, 110);

        // A 2 km disc against the zone.
        double zoneLat = (ZoneSouth + ZoneNorth) / 2;
        double zoneLon = (ZoneWest + ZoneEast) / 2;
        (double DLat, double DLon, string Name)[] placements =
        [
            (0.0, 0.0, "centre"), (0.012, 0.0, "north-edge"), (0.0, 0.03, "east-straddling"),
            (0.025, 0.0, "north-grazing"), (0.03, 0.03, "outside"),
        ];
        foreach ((double dLat, double dLon, string name) in placements)
        {
            yield return new(
                Invariant($"circle-polygon/{name}"),
                "circle-polygon",
                Zone(),
                GeoCircle(zoneLat + dLat, zoneLon + dLon, 2_000));
        }

        yield return new("circle-polygon/needle-north", "circle-polygon", Zone(), GeoCircle(47.660, -122.350, 2_000));

        foreach ((string name, GoldenShape rectangle) in OtherRectangles())
            yield return new(Invariant($"polygon-polygon/{name}"), "polygon-polygon", Zone(), rectangle);

        (double Lat, double Lon, string Name)[] points =
        [
            (47.650, -122.350, "3.6km-north"), (47.609, -122.300, "2.3km-east"), (47.640, -122.300, "north-east-corner"),
            (NeedleLat, NeedleLon, "needle"), (47.609, -122.350, "inside"), (47.590, -122.380, "south-west-corner"),
        ];
        foreach ((double lat, double lon, string name) in points)
            yield return new(Invariant($"point-polygon/{name}"), "point-polygon", Zone(), GeoPoint(lat, lon));
    }

    public static IEnumerable<Input> Planar()
    {
        GoldenShape bay = PlanarRectangle(450, 450, 550, 550);

        yield return new("point-point/3-4-5", "point-point", PlanarPoint(0, 0), PlanarPoint(3, 4));
        yield return new("point-point/diagonal", "point-point", PlanarPoint(150, 150), PlanarPoint(350, 350));
        yield return new("point-point/same", "point-point", PlanarPoint(500, 500), PlanarPoint(500, 500));

        foreach (double radius in (double[])[5, 20, 50, 250])
            yield return new(Invariant($"circle-area/{radius}"), "circle-area", PlanarCircle(0, 0, radius));
        yield return new("polygon-area/bay", "polygon-area", bay);

        foreach ((double x, double y) in ((double, double)[])[(100, 500), (300, 500), (400, 500), (500, 500), (50, 50), (600, 700), (450, 500)])
            yield return new(Invariant($"point-polygon/{x},{y}"), "point-polygon", bay, PlanarPoint(x, y));

        foreach ((double x, double y, double r) in ((double, double, double)[])[(50, 50, 5), (500, 500, 5), (350, 350, 250), (560, 500, 20), (600, 500, 20)])
            yield return new(Invariant($"circle-polygon/{x},{y},r{r}"), "circle-polygon", bay, PlanarCircle(x, y, r));

        foreach ((double x, double y, double r) in ((double, double, double)[])[(0, 0, 50), (40, 0, 50), (51, 0, 50)])
        {
            yield return new(
                Invariant($"point-circle/{x},{y}/r{r}"),
                "point-circle",
                PlanarCircle(0, 0, r),
                PlanarPoint(x, y),
                Math.Sqrt((x * x) + (y * y)) - r);
        }

        foreach ((double x1, double y1, double x2, double y2, double r) in ((double, double, double, double, double)[])
                 [(500, 500, 510, 500, 20), (100, 100, 500, 500, 20), (500, 500, 530, 530, 50), (0, 0, 99, 0, 50), (0, 0, 10, 0, 50)])
        {
            yield return new(
                Invariant($"circle-circle/{x1},{y1}-{x2},{y2}/r{r}"),
                "circle-circle",
                PlanarCircle(x1, y1, r),
                PlanarCircle(x2, y2, r),
                Math.Sqrt(((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1))) - (2 * r));
        }

        yield return new("polygon-polygon/overlap", "polygon-polygon", bay, PlanarRectangle(500, 500, 600, 600));
        yield return new("polygon-polygon/apart", "polygon-polygon", bay, PlanarRectangle(600, 700, 650, 800));
        yield return new("polygon-polygon/contained", "polygon-polygon", bay, PlanarRectangle(475, 475, 525, 525));
    }

    private static Input DiscPair(string prefix, double separation, double azimuth)
    {
        (double lat, double lon) = Direct(NeedleLat, NeedleLon, azimuth, separation);
        return new(
            Invariant($"{prefix}/{separation}m"),
            "circle-circle",
            GeoCircle(NeedleLat, NeedleLon, 2_000),
            GeoCircle(lat, lon, 2_000),
            Inverse(NeedleLat, NeedleLon, lat, lon) - 4_000);
    }

    private static GoldenShape Zone() => GeoRectangle(ZoneSouth, ZoneWest, ZoneNorth, ZoneEast);

    private static IEnumerable<(string Name, GoldenShape Rectangle)> OtherRectangles()
    {
        yield return ("overlapping", GeoRectangle(47.610, -122.350, 47.630, -122.300));
        yield return ("contained", GeoRectangle(47.605, -122.360, 47.610, -122.340));
        yield return ("containing", GeoRectangle(47.500, -122.450, 47.750, -122.200));
        yield return ("apart", GeoRectangle(47.640, -122.300, 47.650, -122.280));
    }

    private static (double Lat, double Lon) Direct(double lat, double lon, double azimuth, double distance)
    {
        Geodesic.WGS84.Direct(lat, lon, azimuth, distance, out double lat2, out double lon2);
        return (lat2, lon2);
    }

    private static double Inverse(double lat1, double lon1, double lat2, double lon2)
    {
        Geodesic.WGS84.Inverse(lat1, lon1, lat2, lon2, out double distance);
        return distance;
    }
}
