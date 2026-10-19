using GeographicLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using Shouldly;

namespace RxSpatial.Tests.Geo;

/// <summary>
/// Laws the geodetic operations must obey for any shapes, checked on seeded random
/// workloads (Seattle-scale, worldwide and across the antimeridian), with GeographicLib as
/// the oracle where a law needs one.
/// </summary>
[TestClass]
public class GeoPropertyTests
{
    private const int Pairs = 3_000;

    [TestMethod]
    public void IntersectsAndDistance_AreSymmetric_AndAgree()
    {
        RandomShapes random = new(1);
        for (int i = 0; i < Pairs; i++)
        {
            GeoPoint centre = random.Centre();
            double size = random.Between(50, 20_000);
            GeoShape a = random.Shape(centre, size);
            GeoShape b = random.Shape(centre, size);

            bool intersects = GeoOperations.Intersects(a, b);
            double distance = GeoOperations.Distance(a, b);

            GeoOperations.Intersects(b, a).ShouldBe(intersects, $"{a} {b}");
            // Polygon pairs are measured in the first polygon's plane, where geodesic edges bow
            // by millimetres at 10 km scale, so the order moves the result by that much.
            GeoOperations.Distance(b, a).ShouldBe(distance, 0.01 + (1e-5 * distance), $"{a} {b}");
            if (intersects)
                distance.ShouldBe(0, $"{a} {b}");
            else
                distance.ShouldBeGreaterThan(0, $"{a} {b}");
        }
    }

    [TestMethod]
    public void PointDistances_ObeyTheTriangleInequality()
    {
        RandomShapes random = new(2);
        for (int i = 0; i < Pairs; i++)
        {
            GeoPoint centre = random.Centre();
            double spread = random.Between(10, 200_000);
            GeoPointShape a = new(random.Near(centre, spread));
            GeoPointShape b = new(random.Near(centre, spread));
            GeoPointShape c = new(random.Near(centre, spread));

            double ac = GeoOperations.Distance(a, c);
            ac.ShouldBeLessThanOrEqualTo(GeoOperations.Distance(a, b) + GeoOperations.Distance(b, c) + 1e-6);
        }
    }

    [TestMethod]
    public void Intersection_LiesInsideBothShapes_AndIsNoLargerThanEither()
    {
        RandomShapes random = new(3);
        int nonEmpty = 0;
        for (int i = 0; i < Pairs; i++)
        {
            GeoPoint centre = random.Centre();
            double size = random.Between(50, 20_000);
            GeoShape a = random.Shape(centre, size);
            GeoShape b = random.Shape(centre, size);

            GeoShape intersection = GeoOperations.Intersection(a, b);

            intersection.IsEmpty.ShouldBe(!GeoOperations.Intersects(a, b), $"{a} {b}");
            double area = GeoOperations.Area(intersection);
            // Each polygon's area is measured in its own plane: allow their (s/R)² difference.
            area.ShouldBeLessThanOrEqualTo((Math.Min(GeoOperations.Area(a), GeoOperations.Area(b)) * (1 + 1e-5)) + 1e-6, $"{a} {b}");
            foreach (GeoPoint sample in SamplesInside(intersection, random))
            {
                nonEmpty++;
                GeoPointShape point = new(sample);
                GeoOperations.Intersects(point, a).ShouldBeTrue($"{sample} in {a} ∩ {b}");
                GeoOperations.Intersects(point, b).ShouldBeTrue($"{sample} in {a} ∩ {b}");
            }
        }

        nonEmpty.ShouldBeGreaterThan(Pairs / 5);
    }

    [TestMethod]
    public void LensArea_IsAtMostTheSmallerDisc_AndMatchesTheAnalyticFormula()
    {
        RandomShapes random = new(4);
        for (int i = 0; i < Pairs; i++)
        {
            GeoPoint centre = random.Centre();
            GeoCircle a = new(centre, random.Between(10, 50_000));
            GeoCircle b = new(random.Near(centre, (a.RadiusMetres * 2) + 1), random.Between(10, 50_000));

            double area = GeoOperations.Area(GeoOperations.Intersection(a, b));

            double smaller = Math.PI * Math.Pow(Math.Min(a.RadiusMetres, b.RadiusMetres), 2);
            area.ShouldBeLessThanOrEqualTo(smaller * (1 + 1e-12));
            Geodesic.WGS84.Inverse(a.Center.Latitude, a.Center.Longitude, b.Center.Latitude, b.Center.Longitude, out double d);
            area.ShouldBe(Lens(d, a.RadiusMetres, b.RadiusMetres), (smaller * 1e-8) + 1e-3);
        }
    }

    [TestMethod]
    public void Bounds_ContainEveryPointOfTheShape()
    {
        RandomShapes random = new(5);
        for (int i = 0; i < 1_000; i++)
        {
            GeoPoint centre = i % 10 == 0 ? new(random.Between(-89, 89), random.Between(-180, 180)) : random.Centre();
            GeoShape shape = random.Shape(centre, random.Between(100, 400_000));
            GeoBounds bounds = GeoOperations.Bounds(shape);
            foreach (GeoPoint point in Outline(shape))
                GeoOperationsTests.Inside(bounds, point).ShouldBeTrue($"{point} outside {bounds} of {shape}");
        }
    }

    [TestMethod]
    public void PointInCircle_GuardedDecision_MatchesTheExactGeodesic()
    {
        // Points within ±1 % of the radius, where FastDistance's guard band has to defer
        // to the exact distance, at latitudes up to 80° and radii up to 250 km.
        RandomShapes random = new(6);
        for (int i = 0; i < 20_000; i++)
        {
            GeoPoint centre = new(random.Between(-80, 80), random.Between(-180, 180));
            GeoCircle circle = new(centre, random.Between(1, 250_000));
            double distance = circle.RadiusMetres * random.Between(0.99, 1.01);
            Geodesic.WGS84.Direct(centre.Latitude, centre.Longitude, random.Between(0, 360), distance, out double lat, out double lon);
            Geodesic.WGS84.Inverse(centre.Latitude, centre.Longitude, lat, lon, out double exact);
            if (Math.Abs(exact - circle.RadiusMetres) < 1e-6)
                continue;

            GeoOperations.Intersects(circle, new GeoPointShape(lat, lon)).ShouldBe(exact <= circle.RadiusMetres, $"{circle} ({lat}, {lon})");
        }
    }

    [TestMethod]
    public void PointToPolygonDistance_MatchesAGeographicLibSearchAlongTheEdges()
    {
        RandomShapes random = new(7);
        int compared = 0;
        for (int i = 0; i < 300; i++)
        {
            GeoPoint centre = random.Centre();
            GeoPolygon polygon = random.Polygon(centre, random.Between(100, 10_000));
            GeoPoint point = random.Near(centre, 30_000);

            double actual = GeoOperations.Distance(new GeoPointShape(point), polygon);
            if (actual == 0)
                continue; // inside: covered by the intersection laws

            // Geodesic edges are slightly curved in the point's plane: ~1e-6 of the distance at 10 km scale.
            double expected = EdgeDistance(polygon, point);
            actual.ShouldBe(expected, Math.Max(0.01, 1e-6 * expected), $"{point} {polygon}");
            compared++;
        }

        compared.ShouldBeGreaterThan(200);
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PointToLargePolygonDistance_StaysWithinTheDocumentedBound()
    {
        // Edges bow in the position's plane by about ρ s² / 2R² (~100 m at this scale), so the
        // nearest edge is refined along the true geodesic: within a micrometre per kilometre.
        RandomShapes random = new(12);
        double worst = 0;
        for (int i = 0; i < 60; i++)
        {
            GeoPoint centre = random.Centre();
            GeoPolygon polygon = random.Polygon(centre, random.Between(20_000, 240_000));
            GeoPoint point = random.Near(centre, 400_000);
            double actual = GeoOperations.Distance(new GeoPointShape(point), polygon);
            if (actual == 0)
                continue;

            double expected = EdgeDistance(polygon, point);
            worst = Math.Max(worst, Math.Abs(actual - expected) / expected);
            actual.ShouldBe(expected, 0.001 + (1e-9 * expected), $"{point} {polygon}");
        }

        TestContext.WriteLine($"worst relative distance error {worst:G3}");
    }

    [TestMethod]
    public void PointInPolygon_IsExactBesideTheGeodesicEdges_UpToTheSizeLimit()
    {
        // Positions 0.5 m either side of each geodesic edge's midpoint: at 500 km the edges bow
        // ~100 m away from their straight image in the plane, so only the exact side test
        // (azimuth at the edge's start) can tell these apart.
        RandomShapes random = new(10);
        for (int i = 0; i < 200; i++)
        {
            double radius = i % 2 == 0 ? random.Between(500, 20_000) : random.Between(20_000, 240_000);
            GeoPolygon polygon = random.Polygon(random.Centre(), radius);
            IReadOnlyList<GeoPoint> vertices = polygon.Vertices;
            for (int k = 0; k < vertices.Count; k++)
            {
                GeoPoint from = vertices[k];
                GeoPoint to = vertices[(k + 1) % vertices.Count];
                Geodesic.WGS84.Inverse(from.Latitude, from.Longitude, to.Latitude, to.Longitude, out double s, out double azimuth, out _);
                Geodesic.WGS84.Direct(from.Latitude, from.Longitude, azimuth, s / 2, out double midLat, out double midLon, out double midAzimuth);

                // Interior is on the left: azimuth − 90°.
                Geodesic.WGS84.Direct(midLat, midLon, midAzimuth - 90, 0.5, out double inLat, out double inLon);
                Geodesic.WGS84.Direct(midLat, midLon, midAzimuth + 90, 0.5, out double outLat, out double outLon);

                GeoOperations.Intersects(polygon, new GeoPointShape(inLat, inLon)).ShouldBeTrue($"edge {k} of {polygon}");
                GeoOperations.Intersects(polygon, new GeoPointShape(outLat, outLon)).ShouldBeFalse($"edge {k} of {polygon}");
                GeoOperations.Distance(polygon, new GeoPointShape(inLat, inLon)).ShouldBe(0);
                GeoOperations.Distance(polygon, new GeoPointShape(outLat, outLon)).ShouldBeGreaterThan(0);
            }
        }
    }

    [TestMethod]
    public void PolygonArea_MatchesGeographicLib()
    {
        RandomShapes random = new(8);
        for (int i = 0; i < 300; i++)
        {
            double radius = i % 2 == 0 ? random.Between(100, 25_000) : random.Between(25_000, 240_000);
            GeoPolygon polygon = random.Polygon(random.Centre(), radius);

            double expected = GeoOperationsTests.GeographicLibArea(polygon.Vertices);

            // Local-plane area: (s/R)²/6 relative at most, i.e. ≤ 0.03 % at 250 km.
            GeoOperations.Area(polygon).ShouldBe(expected, expected * (radius < 25_000 ? 2e-5 : 3e-4), polygon.ToString());
        }
    }

    [TestMethod]
    public void CircleArea_MatchesTheGeodesicCircle()
    {
        foreach (double radius in (double[])[10, 500, 2_000, 50_000, 250_000])
        {
            foreach (double latitude in (double[])[0, 47.6, 80])
            {
                GeoCircle circle = new(new(latitude, 10), radius);
                double expected = GeoOperationsTests.GeographicLibArea(GeoOperations.ToVertices(circle, 4_096));

                GeoOperations.Area(circle).ShouldBe(expected, expected * 2e-4);
            }
        }
    }

    /// <summary>Points strictly inside a shape: the point itself, points within a circle, or convex combinations of a polygon's vertices.</summary>
    private static IEnumerable<GeoPoint> SamplesInside(GeoShape shape, RandomShapes random)
    {
        switch (shape)
        {
            case GeoPointShape p:
                yield return p.Position;
                break;
            case GeoCircle c:
                for (int k = 0; k < 4; k++)
                    yield return Wgs84.Direct(c.Center, random.Between(0, 360), c.RadiusMetres * 0.999 * Math.Sqrt(random.Between(0, 1)));
                break;
            case GeoPolygon polygon:
                LocalPlane plane = polygon.Plane;
                ReadOnlySpan<PlanarPoint> ring = polygon.Ring;
                double cx = 0, cy = 0;
                foreach (PlanarPoint v in ring)
                {
                    cx += v.X / ring.Length;
                    cy += v.Y / ring.Length;
                }

                List<GeoPoint> samples = [];
                for (int k = 0; k < 4; k++)
                {
                    int vertex = random.Next(0, ring.Length);
                    int next = (vertex + 1) % ring.Length;
                    double t = random.Between(0, 1);
                    double shrink = random.Between(0, 0.999);
                    double ex = ring[vertex].X + (t * (ring[next].X - ring[vertex].X));
                    double ey = ring[vertex].Y + (t * (ring[next].Y - ring[vertex].Y));
                    samples.Add(plane.Unproject(new(cx + (shrink * (ex - cx)), cy + (shrink * (ey - cy)))));
                }

                foreach (GeoPoint sample in samples)
                    yield return sample;
                break;
        }
    }

    /// <summary>A circle's or polygon's boundary, densely sampled along its geodesic edges.</summary>
    private static IEnumerable<GeoPoint> Outline(GeoShape shape)
    {
        switch (shape)
        {
            case GeoPointShape p:
                yield return p.Position;
                break;
            case GeoCircle c:
                foreach (GeoPoint v in GeoOperations.ToVertices(c, 720))
                    yield return v;
                break;
            case GeoPolygon polygon:
                foreach (GeoPoint v in DensifiedEdges(polygon, 64))
                    yield return v;
                break;
        }
    }

    private static List<GeoPoint> DensifiedEdges(GeoPolygon polygon, int perEdge)
    {
        List<GeoPoint> points = [];
        IReadOnlyList<GeoPoint> vertices = polygon.Vertices;
        for (int i = 0; i < vertices.Count; i++)
        {
            GeoPoint from = vertices[i];
            GeoPoint to = vertices[(i + 1) % vertices.Count];
            Geodesic.WGS84.Inverse(from.Latitude, from.Longitude, to.Latitude, to.Longitude, out double s, out double azimuth, out _);
            for (int k = 0; k < perEdge; k++)
            {
                Geodesic.WGS84.Direct(from.Latitude, from.Longitude, azimuth, s * k / perEdge, out double lat, out double lon);
                points.Add(new(lat, lon));
            }
        }

        return points;
    }

    /// <summary>
    /// The distance to the nearest point of the polygon's geodesic edges, by golden-section
    /// search along each edge with GeographicLib (the distance along a short edge is unimodal).
    /// </summary>
    private static double EdgeDistance(GeoPolygon polygon, GeoPoint point)
    {
        double best = double.PositiveInfinity;
        IReadOnlyList<GeoPoint> vertices = polygon.Vertices;
        for (int i = 0; i < vertices.Count; i++)
        {
            GeoPoint from = vertices[i];
            GeoPoint to = vertices[(i + 1) % vertices.Count];
            Geodesic.WGS84.Inverse(from.Latitude, from.Longitude, to.Latitude, to.Longitude, out double s, out double azimuth, out _);
            double Along(double t)
            {
                Geodesic.WGS84.Direct(from.Latitude, from.Longitude, azimuth, s * t, out double lat, out double lon);
                Geodesic.WGS84.Inverse(point.Latitude, point.Longitude, lat, lon, out double d);
                return d;
            }

            double low = 0, high = 1;
            double ratio = (Math.Sqrt(5) - 1) / 2;
            for (int k = 0; k < 60; k++)
            {
                double m1 = high - (ratio * (high - low));
                double m2 = low + (ratio * (high - low));
                if (Along(m1) < Along(m2))
                    high = m2;
                else
                    low = m1;
            }

            best = Math.Min(best, Math.Min(Along((low + high) / 2), Math.Min(Along(0), Along(1))));
        }

        return best;
    }

    private static double Lens(double d, double r1, double r2)
    {
        if (d >= r1 + r2)
            return 0;
        if (d <= Math.Abs(r1 - r2))
            return Math.PI * Math.Pow(Math.Min(r1, r2), 2);

        return (r1 * r1 * Math.Acos(((d * d) + (r1 * r1) - (r2 * r2)) / (2 * d * r1)))
            + (r2 * r2 * Math.Acos(((d * d) + (r2 * r2) - (r1 * r1)) / (2 * d * r2)))
            - (0.5 * Math.Sqrt((-d + r1 + r2) * (d + r1 - r2) * (d - r1 + r2) * (d + r1 + r2)));
    }
}
