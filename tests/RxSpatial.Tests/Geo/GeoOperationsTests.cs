// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using GeographicLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using Shouldly;

namespace RxSpatial.Tests.Geo;

/// <summary>
/// Each operation on each pair of geodetic shape kinds, with expected values from
/// GeographicLib or from the analytic geometry.
/// </summary>
[TestClass]
public class GeoOperationsTests
{
    private static readonly GeoPoint Needle = new(47.6205, -122.3493);
    private static readonly GeoCircle Fence = new(Needle, 500);
    private static readonly GeoPolygon Zone = GeoPolygon.Rectangle(47.600, -122.370, 47.618, -122.330);

    [TestMethod]
    public void PointPoint_IntersectOnlyWhenEqual_AndMeasureTheGeodesic()
    {
        GeoPoint other = At(Needle, 37, 1_234.5);

        GeoOperations.Intersects(new GeoPointShape(Needle), new GeoPointShape(Needle)).ShouldBeTrue();
        GeoOperations.Intersects(new GeoPointShape(Needle), new GeoPointShape(other)).ShouldBeFalse();
        GeoOperations.Intersects(new GeoPointShape(90, 0), new GeoPointShape(90, 45)).ShouldBeTrue();
        GeoOperations.Intersects(new GeoPointShape(0, 180), new GeoPointShape(0, -180)).ShouldBeTrue();
        GeoOperations.Distance(new GeoPointShape(Needle), new GeoPointShape(other)).ShouldBe(1_234.5, 1e-6);
        GeoOperations.Intersection(new GeoPointShape(Needle), new GeoPointShape(Needle)).ShouldBeOfType<GeoPointShape>();
        GeoOperations.Intersection(new GeoPointShape(Needle), new GeoPointShape(other)).ShouldBe(GeoEmpty.Instance);
    }

    [TestMethod]
    [DataRow(499.99, true)]
    [DataRow(500.01, false)]
    [DataRow(497.0, true)]
    [DataRow(503.0, false)]
    [DataRow(0.0, true)]
    [DataRow(5_000.0, false)]
    public void PointCircle_IntersectsExactlyAtTheGeodesicRadius(double distance, bool expected)
    {
        for (int azimuth = 0; azimuth < 360; azimuth += 45)
        {
            GeoPointShape point = new(At(Needle, azimuth, distance));

            GeoOperations.Intersects(Fence, point).ShouldBe(expected, $"{azimuth}°");
            GeoOperations.Intersects(point, Fence).ShouldBe(expected, $"{azimuth}°");
            GeoOperations.Distance(point, Fence).ShouldBe(Math.Max(0, distance - 500), 1e-6);
            GeoOperations.Intersection(Fence, point).ShouldBe(expected ? point : GeoEmpty.Instance);
        }
    }

    [TestMethod]
    public void PointPolygon_InsideOnTheEdgeAndOutside()
    {
        GeoPointShape inside = new(47.609, -122.350);
        GeoPointShape onWestEdge = new(47.609, -122.370);
        GeoPointShape north = new(47.650, -122.350);

        GeoOperations.Intersects(Zone, inside).ShouldBeTrue();
        GeoOperations.Intersects(Zone, onWestEdge).ShouldBeTrue();
        GeoOperations.Intersects(Zone, north).ShouldBeFalse();
        GeoOperations.Distance(Zone, inside).ShouldBe(0);
        GeoOperations.Distance(Zone, onWestEdge).ShouldBe(0, 1e-6);

        // The 3 km north edge is a geodesic, bowing L² tan φ / 8R ≈ 0.19 m north of the 47.618°
        // parallel at its middle, so the point due north is that much nearer than the parallel.
        Geodesic.WGS84.Inverse(47.650, -122.350, 47.618, -122.350, out double toParallel);
        GeoOperations.Distance(Zone, north).ShouldBeInRange(toParallel - 0.21, toParallel - 0.17);
        GeoOperations.Intersection(north, Zone).ShouldBe(GeoEmpty.Instance);
    }

    [TestMethod]
    public void PointPolygon_DistanceToACorner_IsTheGeodesicToTheCorner()
    {
        GeoPointShape northEast = new(47.640, -122.300);
        Geodesic.WGS84.Inverse(47.640, -122.300, 47.618, -122.330, out double toCorner);

        GeoOperations.Distance(northEast, Zone).ShouldBe(toCorner, 0.01);
    }

    [TestMethod]
    [DataRow(1_000.0)]
    [DataRow(3_999.0)]
    [DataRow(4_000.0)]
    [DataRow(4_001.0)]
    [DataRow(9_000.0)]
    public void CircleCircle_IntersectWithinTheSumOfRadii(double separation)
    {
        GeoCircle a = new(Needle, 2_000);
        GeoCircle b = new(At(Needle, 63, separation), 2_000);

        GeoOperations.Intersects(a, b).ShouldBe(separation <= 4_000);
        GeoOperations.Distance(a, b).ShouldBe(Math.Max(0, separation - 4_000), 1e-6);
    }

    [TestMethod]
    public void CircleCircle_Intersection_IsTheAnalyticLens()
    {
        GeoCircle a = new(Needle, 2_000);
        GeoCircle b = new(At(Needle, 63, 1_500), 2_000);

        GeoPolygon lens = GeoOperations.Intersection(a, b).ShouldBeOfType<GeoPolygon>();

        double expected = Lens(1_500, 2_000, 2_000);
        GeoOperations.Area(lens).ShouldBe(expected, expected * 1e-9);

        // Arc vertices lie on the circles, ~2.8° apart. The lens is built in the plane centred on
        // a, where a's circle is exact and b's is distorted by about (s/R)², under a millimetre here.
        foreach (GeoPoint vertex in lens.Vertices)
        {
            double toA = Wgs84.Distance(a.Center, vertex);
            double toB = Wgs84.Distance(b.Center, vertex);
            (Math.Abs(toA - 2_000) < 1e-6 || Math.Abs(toB - 2_000) < 1e-3).ShouldBeTrue();
            toA.ShouldBeLessThanOrEqualTo(2_000 + 1e-6);
            toB.ShouldBeLessThanOrEqualTo(2_000 + 1e-3);
        }

        lens.Vertices.Count.ShouldBeInRange(40, 140);
    }

    [TestMethod]
    public void CircleCircle_Intersection_ContainedDiscIsTheSmallerCircle()
    {
        GeoCircle big = new(Needle, 2_000);
        GeoCircle small = new(At(Needle, 10, 500), 300);

        GeoOperations.Intersection(big, small).ShouldBeSameAs(small);
        GeoOperations.Intersection(small, big).ShouldBeSameAs(small);
        GeoOperations.Intersection(big, new GeoCircle(At(Needle, 90, 2_301), 300)).ShouldBe(GeoEmpty.Instance);
    }

    [TestMethod]
    public void CirclePolygon_IntersectionCases()
    {
        GeoPoint zoneCentre = new(47.609, -122.350);

        // A small disc inside the zone is its own intersection; a disc containing the zone gives the zone.
        GeoCircle inside = new(zoneCentre, 300);
        GeoOperations.Intersection(inside, Zone).ShouldBeSameAs(inside);
        GeoCircle covering = new(zoneCentre, 5_000);
        GeoOperations.Intersection(Zone, covering).ShouldBeSameAs(Zone);

        // A disc straddling the north edge: exact area against a high-resolution clip.
        GeoCircle straddling = new(new(47.618, -122.350), 1_000);
        GeoPolygon clipped = GeoOperations.Intersection(straddling, Zone).ShouldBeOfType<GeoPolygon>();
        double highResolution = GeoOperations.Area(GeoOperations.Intersection(
            GeoPolygon.FromVertices(GeoOperations.ToVertices(straddling, 4_096)), Zone));
        GeoOperations.Area(clipped).ShouldBe(highResolution, highResolution * 1e-5);
        GeoOperations.Area(clipped).ShouldBe(Math.PI * 1_000 * 1_000 / 2, 2_000); // about half the disc

        GeoCircle away = new(new(47.640, -122.350), 1_000);
        GeoOperations.Intersects(away, Zone).ShouldBeFalse();
        GeoOperations.Intersection(away, Zone).ShouldBe(GeoEmpty.Instance);
        GeoOperations.Distance(away, Zone).ShouldBe(GeoOperations.Distance(new GeoPointShape(away.Center), Zone) - 1_000, 1e-6);
    }

    [TestMethod]
    public void PolygonPolygon_OverlapContainmentAndDistance()
    {
        GeoPolygon overlapping = GeoPolygon.Rectangle(47.610, -122.350, 47.630, -122.300);
        GeoPolygon contained = GeoPolygon.Rectangle(47.605, -122.360, 47.610, -122.340);
        GeoPolygon apart = GeoPolygon.Rectangle(47.640, -122.300, 47.650, -122.280);
        GeoPolygon touching = GeoPolygon.Rectangle(47.618, -122.370, 47.630, -122.330);

        GeoOperations.Intersects(Zone, overlapping).ShouldBeTrue();
        GeoOperations.Intersects(Zone, contained).ShouldBeTrue();
        GeoOperations.Intersects(Zone, apart).ShouldBeFalse();
        GeoOperations.Intersects(Zone, touching).ShouldBeTrue();

        GeoOperations.Area(GeoOperations.Intersection(Zone, contained)).ShouldBe(GeoOperations.Area(contained), 1e-3);
        GeoOperations.Intersection(Zone, apart).ShouldBe(GeoEmpty.Instance);
        GeoOperations.Intersection(Zone, touching).ShouldBeOfType<GeoPointShape>();

        Geodesic.WGS84.Inverse(47.618, -122.330, 47.640, -122.300, out double cornerToCorner);
        GeoOperations.Distance(Zone, apart).ShouldBe(cornerToCorner, 0.01);
        GeoOperations.Distance(apart, Zone).ShouldBe(cornerToCorner, 0.01);
        GeoOperations.Distance(Zone, overlapping).ShouldBe(0);
    }

    [TestMethod]
    public void Empty_IntersectsNothing_HasNoDistance_AndNoBounds()
    {
        GeoShape[] shapes = [new GeoPointShape(Needle), Fence, Zone, GeoEmpty.Instance];
        foreach (GeoShape shape in shapes)
        {
            GeoOperations.Intersects(GeoEmpty.Instance, shape).ShouldBeFalse();
            GeoOperations.Intersects(shape, GeoEmpty.Instance).ShouldBeFalse();
            double.IsNaN(GeoOperations.Distance(shape, GeoEmpty.Instance)).ShouldBeTrue();
            double.IsNaN(GeoOperations.Distance(GeoEmpty.Instance, shape)).ShouldBeTrue();
            GeoOperations.Intersection(shape, GeoEmpty.Instance).ShouldBe(GeoEmpty.Instance);
        }

        GeoOperations.Area(GeoEmpty.Instance).ShouldBe(0);
        GeoOperations.ToVertices(GeoEmpty.Instance).ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => GeoOperations.Bounds(GeoEmpty.Instance));
    }

    [TestMethod]
    public void Operations_RejectNullShapes()
    {
        Should.Throw<ArgumentNullException>(() => GeoOperations.Intersects(null!, Fence));
        Should.Throw<ArgumentNullException>(() => GeoOperations.Distance(Fence, null!));
        Should.Throw<ArgumentNullException>(() => GeoOperations.Intersection(null!, Fence));
        Should.Throw<ArgumentNullException>(() => GeoOperations.Area(null!));
        Should.Throw<ArgumentNullException>(() => GeoOperations.Bounds(null!));
        Should.Throw<ArgumentNullException>(() => GeoOperations.ToVertices(null!));
        Should.Throw<ArgumentOutOfRangeException>(() => GeoOperations.ToVertices(Fence, 2));
    }

    [TestMethod]
    public void Areas_PointsAreZero_CirclesArePiRSquared_RectanglesMatchGeographicLib()
    {
        GeoOperations.Area(new GeoPointShape(Needle)).ShouldBe(0);
        GeoOperations.Area(Fence).ShouldBe(Math.PI * 500 * 500);
        double expected = GeographicLibArea(Zone.Vertices);
        GeoOperations.Area(Zone).ShouldBe(expected, expected * 1e-6);
    }

    [TestMethod]
    public void Bounds_PointIsDegenerate_CircleAndPolygonContainTheirOutlines()
    {
        GeoOperations.Bounds(new GeoPointShape(Needle))
                     .ShouldBe(new GeoBounds(Needle.Longitude, Needle.Latitude, Needle.Longitude, Needle.Latitude));

        GeoBounds fence = GeoOperations.Bounds(Fence);
        foreach (GeoPoint vertex in GeoOperations.ToVertices(Fence, 4_096))
            Inside(fence, vertex).ShouldBeTrue(vertex.ToString());

        // Tight: within a millimetre-scale margin of the true extremes.
        (fence.MaxLatitude - At(Needle, 0, 500).Latitude).ShouldBeInRange(0, 1e-7);

        GeoBounds zone = GeoOperations.Bounds(Zone);
        zone.MinLongitude.ShouldBe(-122.370, 1e-8);
        zone.MaxLongitude.ShouldBe(-122.330, 1e-8);
        zone.MaxLatitude.ShouldBeGreaterThan(47.618);
        zone.MaxLatitude.ShouldBeLessThan(47.618 + 1e-5);
    }

    [TestMethod]
    public void Antimeridian_OperationsAreSeamless_AndBoundsRunPast180()
    {
        GeoCircle fence = new(new(0, 179.999), 1_000);
        GeoPointShape acrossTheLine = new(0, -179.999);

        GeoOperations.Intersects(fence, acrossTheLine).ShouldBeTrue();
        GeoOperations.Distance(fence, new GeoPointShape(0, -179.98)).ShouldBeGreaterThan(0);
        GeoBounds bounds = GeoOperations.Bounds(fence);
        bounds.MaxLongitude.ShouldBeGreaterThan(180);
        bounds.MinLongitude.ShouldBeLessThan(179.999);

        GeoPolygon straddling = GeoPolygon.FromVertices([new(-0.01, 179.99), new(-0.01, -179.99), new(0.01, -179.99), new(0.01, 179.99)]);
        GeoOperations.Intersects(straddling, new GeoPointShape(0, 180)).ShouldBeTrue();
        GeoOperations.Intersects(straddling, new GeoPointShape(0, -179.995)).ShouldBeTrue();
        GeoOperations.Intersects(straddling, new GeoPointShape(0, 179.98)).ShouldBeFalse();
        GeoOperations.Intersects(straddling, fence).ShouldBeTrue();
        GeoOperations.Bounds(straddling).MaxLongitude.ShouldBe(180.01, 1e-8);
        GeoOperations.Area(straddling).ShouldBe(GeographicLibArea(straddling.Vertices), 1);
    }

    [TestMethod]
    public void Poles_ShapesAroundAPoleSpanAllLongitudes()
    {
        GeoCircle polar = new(new(89.9, 0), 20_000);
        GeoBounds bounds = GeoOperations.Bounds(polar);
        bounds.ShouldBe(new GeoBounds(-180, bounds.MinLatitude, 180, 90));
        GeoOperations.Intersects(polar, new GeoPointShape(90, 123)).ShouldBeTrue();
        GeoOperations.Intersects(polar, new GeoPointShape(89.95, -170)).ShouldBeTrue();

        GeoPolygon cap = GeoPolygon.FromVertices([new(89.9, 0), new(89.9, 90), new(89.9, 180), new(89.9, -90)]);
        GeoBounds capBounds = GeoOperations.Bounds(cap);
        capBounds.MaxLatitude.ShouldBe(90);
        capBounds.MinLongitude.ShouldBe(-180);
        GeoOperations.Intersects(cap, new GeoPointShape(90, 0)).ShouldBeTrue();
    }

    [TestMethod]
    public void ToVertices_CircleIsCounterClockwiseFromEast_AndExactlyOnTheCircle()
    {
        GeoPoint[] ring = GeoOperations.ToVertices(Fence);

        ring.Length.ShouldBe(GeoOperations.DefaultSegments);
        foreach (GeoPoint vertex in ring)
        {
            Geodesic.WGS84.Inverse(Needle.Latitude, Needle.Longitude, vertex.Latitude, vertex.Longitude, out double s);
            s.ShouldBe(500, 1e-6);
        }

        Geodesic.WGS84.Inverse(Needle.Latitude, Needle.Longitude, ring[0].Latitude, ring[0].Longitude, out _, out double azimuth, out _);
        azimuth.ShouldBe(90, 1e-6);
        GeoPolygon.FromVertices(ring).Vertices.ShouldBe(ring); // already counter-clockwise: not reversed
        GeoOperations.ToVertices(Zone).ShouldBe(Zone.Vertices);
        GeoOperations.ToVertices(new GeoPointShape(Needle)).ShouldBe([Needle]);
    }

    [TestMethod]
    public void ToVertices_IntoSpan_WritesTheSameOutline_AndChecksTheDestination()
    {
        GeoPoint[] buffer = new GeoPoint[70];
        GeoShape[] shapes = [Fence, Zone, new GeoPointShape(Needle), GeoEmpty.Instance];
        foreach (GeoShape shape in shapes)
        {
            int count = GeoOperations.ToVertices(shape, buffer, 64);
            buffer[..count].ShouldBe(GeoOperations.ToVertices(shape, 64));
        }

        Should.Throw<ArgumentException>(() => GeoOperations.ToVertices(Fence, new GeoPoint[63], 64));
        Should.Throw<ArgumentOutOfRangeException>(() => GeoOperations.ToVertices(Fence, buffer, 2));
        Should.Throw<ArgumentNullException>(() => GeoOperations.ToVertices(null!, buffer));
    }

    internal static GeoPoint At(GeoPoint from, double azimuth, double distance)
    {
        Geodesic.WGS84.Direct(from.Latitude, from.Longitude, azimuth, distance, out double lat, out double lon);
        return new(lat, lon);
    }

    internal static double GeographicLibArea(IEnumerable<GeoPoint> ring)
    {
        PolygonArea area = new(Geodesic.WGS84, polyline: false);
        foreach (GeoPoint vertex in ring)
            area.AddPoint(vertex.Latitude, vertex.Longitude);

        (_, _, double result) = area.Compute(false, true);
        return Math.Abs(result);
    }

    internal static bool Inside(GeoBounds bounds, GeoPoint point)
    {
        double centre = (bounds.MinLongitude + bounds.MaxLongitude) / 2;
        double longitude = centre + Math.IEEERemainder(point.Longitude - centre, 360);
        return point.Latitude >= bounds.MinLatitude && point.Latitude <= bounds.MaxLatitude
            && longitude >= bounds.MinLongitude && longitude <= bounds.MaxLongitude;
    }

    private static double Lens(double d, double r1, double r2)
        => (r1 * r1 * Math.Acos(((d * d) + (r1 * r1) - (r2 * r2)) / (2 * d * r1)))
        + (r2 * r2 * Math.Acos(((d * d) + (r2 * r2) - (r1 * r1)) / (2 * d * r2)))
        - (0.5 * Math.Sqrt((-d + r1 + r2) * (d + r1 - r2) * (d - r1 + r2) * (d + r1 + r2)));
}
