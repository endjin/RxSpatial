using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using Shouldly;

namespace RxSpatial.Tests.Geo;

/// <summary>Constructing and validating geodetic shapes.</summary>
[TestClass]
public class GeoShapeTests
{
    private const string Zone = "POLYGON ((-122.37 47.6, -122.33 47.6, -122.33 47.618, -122.37 47.618, -122.37 47.6))";

    [TestMethod]
    public void Rectangle_RunsCounterClockwiseFromTheSouthWestCorner()
    {
        GeoPolygon zone = GeoPolygon.Rectangle(47.600, -122.370, 47.618, -122.330);

        zone.Vertices.ShouldBe([new(47.600, -122.370), new(47.600, -122.330), new(47.618, -122.330), new(47.618, -122.370)]);
        zone.ToString().ShouldBe(Zone);
    }

    [TestMethod]
    public void FromWkt_ReadsLongitudeFirst_AndRoundTripsThroughToString()
    {
        GeoPolygon zone = GeoPolygon.FromWkt(Zone);

        zone.Vertices.ShouldBe(GeoPolygon.Rectangle(47.600, -122.370, 47.618, -122.330).Vertices);
        GeoPolygon.FromWkt(zone.ToString()).Vertices.ShouldBe(zone.Vertices);
    }

    [TestMethod]
    [DataRow("polygon((-122.37 47.6,-122.33 47.6,-122.33 47.618,-122.37 47.618))")]
    [DataRow("  POLYGON  (( -122.37   47.6 , -122.33 47.6, -122.33 47.618, -122.37 47.618 ))  ")]
    [DataRow("POLYGON ((-1.2237E2 4.76E1, -122.33 47.6, -122.33 47.618, -122.37 47.618))")]
    public void FromWkt_AcceptsCaseSpacingExponentsAndAnOpenRing(string wkt)
        => GeoPolygon.FromWkt(wkt).Vertices.Count.ShouldBe(4);

    [TestMethod]
    public void FromWkt_ReversesAClockwiseRing()
    {
        GeoPolygon clockwise = GeoPolygon.FromWkt(
            "POLYGON ((-122.37 47.6, -122.37 47.618, -122.33 47.618, -122.33 47.6, -122.37 47.6))");

        clockwise.Vertices.ShouldBe([new(47.600, -122.330), new(47.618, -122.330), new(47.618, -122.370), new(47.600, -122.370)]);
        GeoOperations.Area(clockwise).ShouldBe(GeoOperations.Area(GeoPolygon.FromWkt(Zone)), 1e-6);
    }

    [TestMethod]
    [DataRow("POINT (1 2)", "form")]
    [DataRow("POLYGON (1 2, 3 4, 5 6)", "form")]
    [DataRow("POLYGON ((0 0, 1 0, 1 1, 0 1, 0 0), (0.2 0.2, 0.4 0.2, 0.4 0.4, 0.2 0.2))", "holes")]
    [DataRow("POLYGON ((0 0 1, 1 0 1, 1 1 1, 0 0 1))", "coordinate pair")]
    [DataRow("POLYGON ((0 0, 1 x, 1 1, 0 0))", "coordinate pair")]
    [DataRow("POLYGON ((0 95, 1 0, 1 1, 0 95))", "valid longitude and latitude")]
    [DataRow("POLYGON EMPTY", "form")]
    public void FromWkt_RejectsTextThatIsNotASingleRingPolygon(string wkt, string reason)
        => Should.Throw<ArgumentException>(() => GeoPolygon.FromWkt(wkt)).Message.ShouldContain(reason);

    [TestMethod]
    public void FromVertices_RejectsConcaveRings()
    {
        // An L-shaped fence: the corner at (0.005, 0.005) turns the wrong way.
        ArgumentException error = Should.Throw<ArgumentException>(() => GeoPolygon.FromVertices(
            [new(0, 0), new(0, 0.01), new(0.005, 0.01), new(0.005, 0.005), new(0.01, 0.005), new(0.01, 0)]));

        error.Message.ShouldContain("concave");
    }

    [TestMethod]
    public void FromVertices_RejectsSelfIntersectingRings()
    {
        // A bow tie turns both ways; a pentagram turns the same way at every vertex but winds twice.
        Should.Throw<ArgumentException>(() => GeoPolygon.FromVertices(
            [new(0, 0), new(0.01, 0.01), new(0, 0.01), new(0.01, 0)]));

        LocalPlane plane = new(new(10, 10));
        GeoPoint[] pentagram = [.. Enumerable.Range(0, 5).Select(i =>
        {
            double angle = (Math.PI / 2) + (4 * Math.PI * i / 5);
            return plane.Unproject(new(1_000 * Math.Cos(angle), 1_000 * Math.Sin(angle)));
        })];
        Should.Throw<ArgumentException>(() => GeoPolygon.FromVertices(pentagram)).Message.ShouldContain("self-intersecting");
    }

    [TestMethod]
    public void FromVertices_RejectsDegenerateRings()
    {
        Should.Throw<ArgumentException>(() => GeoPolygon.FromVertices([new(0, 0), new(0, 0.01)])).Message.ShouldContain("three");
        Should.Throw<ArgumentException>(() => GeoPolygon.FromVertices([new(0, 0), new(0, 0.01), new(0, 0.01), new(0, 0)]))
              .Message.ShouldContain("three");
        Should.Throw<ArgumentException>(() => GeoPolygon.FromVertices([new(0, 0), new(0, 0.01), new(0, 0.02)]))
              .Message.ShouldContain("no area");
    }

    [TestMethod]
    public void FromVertices_DropsRepeatedAndClosingVertices()
    {
        GeoPolygon triangle = GeoPolygon.FromVertices(
            [new(0, 0), new(0, 0), new(0, 0.01), new(0.01, 0.01), new(0, 0)]);

        triangle.Vertices.Count.ShouldBe(3);
    }

    [TestMethod]
    public void Shapes_LargerThanFiveHundredKilometres_AreRejected()
    {
        Should.Throw<ArgumentException>(() => GeoPolygon.Rectangle(40, -5, 45, 5)).Message.ShouldContain("across");
        Should.Throw<ArgumentException>(() => GeoPolygon.FromVertices([new(0, 0), new(0, 90), new(45, 45)]));
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoCircle(new(0, 0), 250_001));
        _ = new GeoCircle(new(0, 0), GeoShape.MaxExtent / 2);
        _ = GeoPolygon.Rectangle(47, -122, 49, -119);
    }

    [TestMethod]
    public void Rectangle_RejectsCornersOutOfOrder()
    {
        Should.Throw<ArgumentException>(() => GeoPolygon.Rectangle(47.618, -122.370, 47.600, -122.330));
        Should.Throw<ArgumentException>(() => GeoPolygon.Rectangle(47.600, -122.330, 47.618, -122.370));
        Should.Throw<ArgumentOutOfRangeException>(() => GeoPolygon.Rectangle(89.99, 0, 91, 1));
    }

    [TestMethod]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    public void Circle_RejectsRadiiThatAreNotPositiveAndFinite(double radius)
        => Should.Throw<ArgumentOutOfRangeException>(() => new GeoCircle(new(0, 0), radius));

    [TestMethod]
    public void Shapes_DescribeThemselvesAsWkt()
    {
        new GeoPointShape(47.5, -122.25).ToString().ShouldBe("POINT (-122.25 47.5)");
        new GeoCircle(new(47.5, -122.25), 500).ToString().ShouldBe("CIRCLE (-122.25 47.5, 500)");
        GeoEmpty.Instance.ToString().ShouldBe("POLYGON EMPTY");
        GeoEmpty.Instance.IsEmpty.ShouldBeTrue();
        new GeoPointShape(0, 0).IsEmpty.ShouldBeFalse();
    }

    [TestMethod]
    public void FromVertices_AcceptsAnyEnumerable()
    {
        List<GeoPoint> vertices = [new(0, 0), new(0, 0.01), new(0.01, 0.01)];

        GeoPolygon.FromVertices(vertices).Vertices.Count.ShouldBe(3);
        Should.Throw<ArgumentNullException>(() => GeoPolygon.FromVertices((IEnumerable<GeoPoint>)null!));
    }
}
