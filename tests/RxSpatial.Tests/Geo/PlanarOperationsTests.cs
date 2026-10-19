using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using Shouldly;

namespace RxSpatial.Tests.Geo;

/// <summary>The planar twin: exact Euclidean results, construction rules and laws.</summary>
[TestClass]
public class PlanarOperationsTests
{
    // The warehouse loading bay of the Examples and tests.
    private static readonly PlanarPolygon Bay = PlanarPolygon.FromWkt("POLYGON ((450 450, 550 450, 550 550, 450 550, 450 450))");

    [TestMethod]
    public void PointPolygon_DistancesAlongTheExamplesRoute_AreExact()
    {
        double[] distances = [.. ((double[])[100, 300, 400, 500]).Select(x => PlanarOperations.Distance(new PlanarPointShape(x, 500), Bay))];

        distances.ShouldBe([350, 150, 50, 0]);
        PlanarOperations.Intersects(Bay, new PlanarPointShape(450, 500)).ShouldBeTrue(); // on the edge
        PlanarOperations.Distance(Bay, new PlanarPointShape(600, 700)).ShouldBe(Math.Sqrt((50 * 50) + (150 * 150)));
    }

    [TestMethod]
    public void PointPoint_AndPointCircle()
    {
        PlanarPointShape origin = new(0, 0);
        PlanarOperations.Distance(origin, new PlanarPointShape(3, 4)).ShouldBe(5);
        PlanarOperations.Intersects(origin, new PlanarPointShape(0, 0)).ShouldBeTrue();
        PlanarOperations.Intersects(origin, new PlanarPointShape(0, 1e-12)).ShouldBeFalse();

        PlanarCircle disc = new(new(0, 0), 50);
        PlanarOperations.Intersects(disc, new PlanarPointShape(30, 40)).ShouldBeTrue(); // on the circle
        PlanarOperations.Intersects(disc, new PlanarPointShape(30, 40.001)).ShouldBeFalse();
        PlanarOperations.Distance(new PlanarPointShape(60, 80), disc).ShouldBe(50);
        PlanarOperations.Intersection(disc, origin).ShouldBeSameAs(origin);
    }

    [TestMethod]
    public void CircleCircle_LensContainmentTangencyAndDistance()
    {
        PlanarCircle a = new(new(0, 0), 1);
        PlanarCircle b = new(new(1, 0), 1);

        // Two unit discs one radius apart: 2π/3 − √3/2.
        PlanarPolygon lens = PlanarOperations.Intersection(a, b).ShouldBeOfType<PlanarPolygon>();
        PlanarOperations.Area(lens).ShouldBe((2 * Math.PI / 3) - (Math.Sqrt(3) / 2), 1e-12);
        foreach (PlanarPoint vertex in lens.Vertices)
            Math.Min(Math.Abs(vertex.DistanceTo(new(0, 0)) - 1), Math.Abs(vertex.DistanceTo(new(1, 0)) - 1)).ShouldBeLessThan(1e-12);

        PlanarCircle inner = new(new(0.2, 0), 0.5);
        PlanarOperations.Intersection(a, inner).ShouldBeSameAs(inner);

        PlanarPointShape touch = PlanarOperations.Intersection(a, new PlanarCircle(new(2, 0), 1)).ShouldBeOfType<PlanarPointShape>();
        touch.Position.ShouldBe(new PlanarPoint(1, 0));

        PlanarOperations.Distance(a, new PlanarCircle(new(5, 0), 1)).ShouldBe(3);
        PlanarOperations.Intersection(a, new PlanarCircle(new(5, 0), 1)).ShouldBe(PlanarEmpty.Instance);
    }

    [TestMethod]
    public void CirclePolygon_AreasAreExact()
    {
        // Centred on a corner: a quarter disc. Centred on an edge: a half disc.
        PlanarOperations.Area(PlanarOperations.Intersection(new PlanarCircle(new(450, 450), 20), Bay))
                        .ShouldBe(Math.PI * 400 / 4, 1e-9);
        PlanarOperations.Area(PlanarOperations.Intersection(Bay, new PlanarCircle(new(500, 450), 20)))
                        .ShouldBe(Math.PI * 400 / 2, 1e-9);

        // Disc inside the bay is itself; a disc covering the bay gives the bay.
        PlanarCircle inside = new(new(500, 500), 5);
        PlanarOperations.Intersection(inside, Bay).ShouldBeSameAs(inside);
        PlanarOperations.Intersection(new PlanarCircle(new(500, 500), 100), Bay).ShouldBeSameAs(Bay);

        // A disc reaching across a corner: the exact area matches a 16,384-gon clip.
        PlanarCircle corner = new(new(560, 560), 30);
        double exact = PlanarOperations.Area(PlanarOperations.Intersection(corner, Bay));
        double fine = PlanarOperations.Area(PlanarOperations.Intersection(PlanarPolygon.FromVertices(PlanarOperations.ToVertices(corner, 16_384)), Bay));
        exact.ShouldBe(fine, exact * 1e-6);

        PlanarOperations.Distance(new PlanarCircle(new(600, 500), 20), Bay).ShouldBe(30);
        PlanarOperations.Intersection(new PlanarCircle(new(570, 500), 20), Bay).ShouldBeOfType<PlanarPointShape>()
                        .Position.ShouldBe(new PlanarPoint(550, 500));
    }

    [TestMethod]
    public void PolygonPolygon_OverlapTouchContainmentAndDistance()
    {
        PlanarPolygon overlap = PlanarPolygon.Rectangle(500, 500, 600, 600);
        PlanarOperations.Area(PlanarOperations.Intersection(Bay, overlap)).ShouldBe(2_500);
        PlanarOperations.Intersection(Bay, PlanarPolygon.Rectangle(550, 450, 650, 550)).ShouldBeOfType<PlanarPointShape>();
        PlanarOperations.Intersects(Bay, PlanarPolygon.Rectangle(550, 450, 650, 550)).ShouldBeTrue();
        PlanarOperations.Area(PlanarOperations.Intersection(Bay, PlanarPolygon.Rectangle(475, 475, 525, 525))).ShouldBe(2_500);
        PlanarOperations.Distance(Bay, PlanarPolygon.Rectangle(600, 700, 650, 800)).ShouldBe(Math.Sqrt((50 * 50) + (150 * 150)));
        PlanarOperations.Intersection(Bay, PlanarPolygon.Rectangle(600, 700, 650, 800)).ShouldBe(PlanarEmpty.Instance);
    }

    [TestMethod]
    public void Empty_AndBoundsAndVertices()
    {
        double.IsNaN(PlanarOperations.Distance(Bay, PlanarEmpty.Instance)).ShouldBeTrue();
        PlanarOperations.Intersects(PlanarEmpty.Instance, Bay).ShouldBeFalse();
        PlanarOperations.Intersection(Bay, PlanarEmpty.Instance).ShouldBe(PlanarEmpty.Instance);
        PlanarOperations.Area(PlanarEmpty.Instance).ShouldBe(0);
        PlanarOperations.ToVertices(PlanarEmpty.Instance).ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(() => PlanarOperations.Bounds(PlanarEmpty.Instance));
        PlanarEmpty.Instance.IsEmpty.ShouldBeTrue();

        PlanarOperations.Bounds(Bay).ShouldBe(new PlanarBounds(450, 450, 550, 550));
        PlanarOperations.Bounds(new PlanarCircle(new(1, 2), 3)).ShouldBe(new PlanarBounds(-2, -1, 4, 5));
        PlanarOperations.Bounds(new PlanarPointShape(1, 2)).ShouldBe(new PlanarBounds(1, 2, 1, 2));

        PlanarPoint[] circle = PlanarOperations.ToVertices(new PlanarCircle(new(0, 0), 2));
        circle.Length.ShouldBe(128);
        circle[0].ShouldBe(new PlanarPoint(2, 0));
        circle[32].X.ShouldBe(0, 1e-12);
        circle[32].Y.ShouldBe(2, 1e-12);
        PlanarOperations.ToVertices(new PlanarPointShape(1, 2)).ShouldBe([new PlanarPoint(1, 2)]);
        Should.Throw<ArgumentOutOfRangeException>(() => PlanarOperations.ToVertices(Bay, 2));
    }

    [TestMethod]
    public void Construction_ValidatesAndOrients()
    {
        PlanarPolygon clockwise = PlanarPolygon.FromVertices([new(0, 0), new(0, 1), new(1, 1), new(1, 0)]);
        clockwise.Vertices.ShouldBe([new(1, 0), new(1, 1), new(0, 1), new(0, 0)]);
        PlanarOperations.Area(clockwise).ShouldBe(1);
        Bay.ToString().ShouldBe("POLYGON ((450 450, 550 450, 550 550, 450 550, 450 450))");

        Should.Throw<ArgumentException>(() => PlanarPolygon.FromVertices([new(0, 0), new(2, 0), new(2, 2), new(1, 1), new(0, 2)]))
              .Message.ShouldContain("concave");
        Should.Throw<ArgumentException>(() => PlanarPolygon.FromVertices([new(0, 0), new(1, 1), new(0, 1), new(1, 0)]));
        Should.Throw<ArgumentException>(() => PlanarPolygon.FromVertices([new(0, 0), new(1, 1), new(2, 2)])).Message.ShouldContain("no area");
        Should.Throw<ArgumentException>(() => PlanarPolygon.FromVertices([new(0, 0), new(double.NaN, 1), new(2, 2)]));
        Should.Throw<ArgumentException>(() => PlanarPolygon.Rectangle(0, 0, 0, 5));
        Should.Throw<ArgumentException>(() => PlanarPolygon.FromWkt("POLYGON ((0 0, 1 0, 1 1, 0 0), (0 0, 1 0, 1 1, 0 0))"));
        Should.Throw<ArgumentException>(() => new PlanarPointShape(double.PositiveInfinity, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new PlanarCircle(new(0, 0), 0));
        Should.Throw<ArgumentException>(() => new PlanarCircle(new(double.NaN, 0), 1));
        PlanarPolygon.Rectangle(5, 5, 0, 0).Vertices.Count.ShouldBe(4); // either corner order
        PlanarPolygon.FromVertices(new List<PlanarPoint> { new(0, 0), new(1, 0), new(0, 1) }).Vertices.Count.ShouldBe(3);
        new PlanarCircle(new(1, 2), 3).ToString().ShouldBe("CIRCLE (1 2, 3)");
        new PlanarPointShape(1.5, 2).ToString().ShouldBe("POINT (1.5 2)");
    }

    [TestMethod]
    public void Laws_HoldForRandomShapes()
    {
        RandomShapes random = new(9);
        for (int i = 0; i < 5_000; i++)
        {
            PlanarShape a = random.Planar(0, 0, 100);
            PlanarShape b = random.Planar(0, 0, 100);

            bool intersects = PlanarOperations.Intersects(a, b);
            double distance = PlanarOperations.Distance(a, b);
            PlanarOperations.Intersects(b, a).ShouldBe(intersects);
            PlanarOperations.Distance(b, a).ShouldBe(distance, 1e-9);
            (distance == 0).ShouldBe(intersects, $"{a} {b}");

            PlanarShape intersection = PlanarOperations.Intersection(a, b);
            intersection.IsEmpty.ShouldBe(!intersects);
            double area = PlanarOperations.Area(intersection);
            area.ShouldBeLessThanOrEqualTo((Math.Min(PlanarOperations.Area(a), PlanarOperations.Area(b)) * (1 + 1e-12)) + 1e-9);
            if (intersection is PlanarPolygon polygon)
            {
                PlanarPoint centroid = new(polygon.Vertices.Average(v => v.X), polygon.Vertices.Average(v => v.Y));
                foreach (PlanarPoint vertex in polygon.Vertices)
                {
                    PlanarPointShape sample = new(centroid.X + (0.999 * (vertex.X - centroid.X)), centroid.Y + (0.999 * (vertex.Y - centroid.Y)));
                    PlanarOperations.Intersects(sample, a).ShouldBeTrue($"{a} {b}");
                    PlanarOperations.Intersects(sample, b).ShouldBeTrue($"{a} {b}");
                }

                // The result is itself a valid convex polygon.
                _ = PlanarPolygon.FromVertices(polygon.Vertices);
            }
        }
    }
}
