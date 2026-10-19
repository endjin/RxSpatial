using System.Globalization;
using GeographicLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Geo;
using RxSpatial.Tests.Golden;
using Shouldly;

namespace RxSpatial.Tests.Geo;

/// <summary>
/// The clean-room shape maths against SQL Server's recorded results (tests/golden). They read
/// JSON, so they run on any operating system. The budget:
/// <list type="bullet">
/// <item>point–point distance within max(0.01 %, 1 cm);</item>
/// <item>point–circle and point–polygon distances within 0.5 m; between two areal shapes, 0.5 m
/// plus the sag of SQL Server's 128-gon chords, r(1 − cos(π/128)), for each circle;</item>
/// <item>areas within 0.15 %. SQL Server's circles are inscribed 128-gons, whose area falls
/// short of a disc by 0.04 % overall but by much more for a thin sliver; slivers under 1 % of
/// the smaller shape's area are compared with an independent analytic value instead. Where an
/// area misses 0.15 % only because of the 128-gon (a disc clipped to a small cap), the same
/// intersection with our circle replaced by its inscribed 128-gon must match SQL Server within
/// 0.15 %, and the exact area must exceed it;</item>
/// <item>Intersects agrees for every case more than 0.5 m from a boundary. A boundary here
/// includes SQL Server's own: its 128-gon's chords sag up to r(1 − cos(π/128)) inside the
/// true circle, so that sag is added to the 0.5 m for circles.</item>
/// </list>
/// </summary>
[TestClass]
public class GoldenFidelityTests
{
    private const double BoundaryMargin = 0.5;
    private const double DistanceBudget = 0.5;
    private const double AreaBudget = 0.0015;
    private const double ThinFraction = 0.01;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Geodetic_MatchesSqlServerWithinBudget()
    {
        GoldenFile file = GoldenFile.Load("geodetic.json");
        Report report = new();
        foreach (GoldenCase golden in file.Cases)
        {
            GeoShape a = ToGeo(golden.A);
            if (golden.B is null)
            {
                report.Area(golden, GeoOperations.Area(a), golden.Sql.Area!.Value);
                continue;
            }

            GeoShape b = ToGeo(golden.B);
            double distance = GeoOperations.Distance(a, b);
            double sqlDistance = golden.Sql.Distance!.Value;
            double budget = golden.Category switch
            {
                "point-point" => Math.Max(1e-4 * sqlDistance, 0.01),
                "point-circle" or "point-polygon" => DistanceBudget,

                // Between two areal shapes SQL Server measures from its 128-gon's chords.
                _ => DistanceBudget + Sag(golden.A) + Sag(golden.B),
            };
            report.Check(golden, "distance", Math.Abs(distance - sqlDistance), budget);

            double margin = BoundaryMargin + Sag(golden.A) + Sag(golden.B);
            if (golden.Clearance is not { } clearance || Math.Abs(clearance) > margin)
                report.Same(golden, GeoOperations.Intersects(a, b), golden.Sql.Intersects!.Value);

            if (golden.A.Type != "point" && golden.B.Type != "point")
            {
                double area = GeoOperations.Area(GeoOperations.Intersection(a, b));
                double smaller = Math.Min(GeoOperations.Area(a), GeoOperations.Area(b));
                double sqlArea = golden.Sql.IntersectionArea!.Value;
                if (golden.Category == "circle-circle" && area > 0 && Math.Max(area, sqlArea) < ThinFraction * smaller)
                    report.ThinArea(golden, area, AnalyticGeodeticArea(golden));
                else if (sqlArea == 0 || Math.Abs(area - sqlArea) <= AreaBudget * sqlArea || golden.Category == "polygon-polygon")
                    report.Area(golden, area, sqlArea);
                else
                    report.Polygonised(golden, area, GeoOperations.Area(GeoOperations.Intersection(Polygonised(a), Polygonised(b))), sqlArea);
            }
        }

        report.Write(TestContext);
        report.Failures.ShouldBeEmpty();
    }

    [TestMethod]
    public void Planar_MatchesSqlServerWithinBudget()
    {
        GoldenFile file = GoldenFile.Load("planar.json");
        Report report = new();
        foreach (GoldenCase golden in file.Cases)
        {
            PlanarShape a = ToPlanar(golden.A);
            if (golden.B is null)
            {
                report.Area(golden, PlanarOperations.Area(a), golden.Sql.Area!.Value);
                continue;
            }

            PlanarShape b = ToPlanar(golden.B);
            double distance = PlanarOperations.Distance(a, b);
            double sqlDistance = golden.Sql.Distance!.Value;

            // Exact where SQL Server is exact (no circles); otherwise SQL's buffer sag.
            double budget = 1e-9 * Math.Max(1, sqlDistance) + Sag(golden.A) + Sag(golden.B);
            report.Check(golden, "distance", Math.Abs(distance - sqlDistance), budget);

            double margin = 1e-9 + Sag(golden.A) + Sag(golden.B);
            if (golden.Clearance is not { } clearance || Math.Abs(clearance) > margin)
                report.Same(golden, PlanarOperations.Intersects(a, b), golden.Sql.Intersects!.Value);

            if (golden.A.Type != "point" && golden.B.Type != "point")
            {
                double area = PlanarOperations.Area(PlanarOperations.Intersection(a, b));
                double smaller = Math.Min(PlanarOperations.Area(a), PlanarOperations.Area(b));
                double sqlArea = golden.Sql.IntersectionArea!.Value;
                if (golden.Category == "circle-circle" && area > 0 && Math.Max(area, sqlArea) < ThinFraction * smaller)
                    report.ThinArea(golden, area, AnalyticPlanarArea(golden));
                else
                    report.Area(golden, area, sqlArea);
            }
        }

        report.Write(TestContext);
        report.Failures.ShouldBeEmpty();
    }

    internal static GeoShape ToGeo(GoldenShape shape) => shape.Type switch
    {
        "point" => new GeoPointShape(shape.Lat!.Value, shape.Lon!.Value),
        "circle" => new GeoCircle(new(shape.Lat!.Value, shape.Lon!.Value), shape.Radius!.Value),
        "rectangle" => GeoPolygon.Rectangle(shape.South!.Value, shape.West!.Value, shape.North!.Value, shape.East!.Value),
        _ => throw new InvalidDataException(shape.Type),
    };

    internal static PlanarShape ToPlanar(GoldenShape shape) => shape.Type switch
    {
        "point" => new PlanarPointShape(shape.X!.Value, shape.Y!.Value),
        "circle" => new PlanarCircle(new(shape.X!.Value, shape.Y!.Value), shape.Radius!.Value),
        "rectangle" => PlanarPolygon.Rectangle(shape.MinX!.Value, shape.MinY!.Value, shape.MaxX!.Value, shape.MaxY!.Value),
        _ => throw new InvalidDataException(shape.Type),
    };

    /// <summary>A circle as SQL Server buffers it: the 128-gon inscribed in it. Other shapes unchanged.</summary>
    private static GeoShape Polygonised(GeoShape shape)
        => shape is GeoCircle ? GeoPolygon.FromVertices(GeoOperations.ToVertices(shape, 128)) : shape;

    /// <summary>How far SQL Server's 128-gon chords sag inside a circle; 0 for other shapes.</summary>
    private static double Sag(GoldenShape shape) => shape.Type == "circle" ? shape.Radius!.Value * (1 - Math.Cos(Math.PI / 128)) : 0;

    /// <summary>The lens area from GeographicLib's distance between the centres and the textbook formula.</summary>
    private static double AnalyticGeodeticArea(GoldenCase golden)
    {
        golden.Category.ShouldBe("circle-circle", $"{golden.Id}: thin slivers have an analytic value only for lenses");
        Geodesic.WGS84.Inverse(golden.A.Lat!.Value, golden.A.Lon!.Value, golden.B!.Lat!.Value, golden.B.Lon!.Value, out double d);
        return Lens(d, golden.A.Radius!.Value, golden.B.Radius!.Value);
    }

    private static double AnalyticPlanarArea(GoldenCase golden)
    {
        if (golden.Category != "circle-circle")
            return 0;

        double dx = golden.B!.X!.Value - golden.A.X!.Value;
        double dy = golden.B.Y!.Value - golden.A.Y!.Value;
        return Lens(Math.Sqrt((dx * dx) + (dy * dy)), golden.A.Radius!.Value, golden.B.Radius!.Value);
    }

    /// <summary>The area of two overlapping discs: r²·acos terms minus the kite (an independent implementation).</summary>
    private static double Lens(double d, double r1, double r2)
    {
        if (d >= r1 + r2)
            return 0;

        double a = (r1 * r1 * Math.Acos(((d * d) + (r1 * r1) - (r2 * r2)) / (2 * d * r1)))
            + (r2 * r2 * Math.Acos(((d * d) + (r2 * r2) - (r1 * r1)) / (2 * d * r2)));
        return a - (0.5 * Math.Sqrt((-d + r1 + r2) * (d + r1 - r2) * (d - r1 + r2) * (d + r1 + r2)));
    }

    /// <summary>Collects deltas per category and check, and the budget failures.</summary>
    private sealed class Report
    {
        private readonly SortedDictionary<string, (double Worst, double Budget, string Id, int Count)> worst = new(StringComparer.Ordinal);

        public List<string> Failures { get; } = [];

        public void Check(GoldenCase golden, string check, double delta, double budget)
        {
            string key = $"{golden.Category} {check}";
            (double Worst, double Budget, string Id, int Count) current = worst.GetValueOrDefault(key, (-1, budget, "", 0));
            worst[key] = delta > current.Worst
                ? (delta, budget, golden.Id, current.Count + 1)
                : current with { Count = current.Count + 1 };
            if (!(delta <= budget))
                Failures.Add(string.Create(CultureInfo.InvariantCulture, $"{golden.Id} {check}: delta {delta:G6} over budget {budget:G6}"));
        }

        public void Same(GoldenCase golden, bool actual, bool expected)
            => Check(golden, "intersects", actual == expected ? 0 : 1, 0);

        public void Area(GoldenCase golden, double actual, double sql)
            => Check(golden, "area (relative)", sql == 0 ? Math.Abs(actual) : Math.Abs(actual - sql) / sql, sql == 0 ? 1e-6 : AreaBudget);

        /// <summary>
        /// Where SQL Server's polygonised circle is what differs: the same intersection with
        /// our circle polygonised as a 128-gon must match SQL Server, and the exact area must
        /// exceed it (an inscribed polygon loses area).
        /// </summary>
        public void Polygonised(GoldenCase golden, double exact, double polygonised, double sql)
        {
            Check(golden, "area, circles as 128-gons (relative)", Math.Abs(polygonised - sql) / sql, AreaBudget);
            Check(golden, "exact area exceeds 128-gon area", exact > polygonised ? 0 : 1, 0);
        }

        public void ThinArea(GoldenCase golden, double actual, double analytic)
            => Check(golden, "thin area vs analytic (relative)", analytic == 0 ? Math.Abs(actual) : Math.Abs(actual - analytic) / analytic, analytic == 0 ? 1e-6 : AreaBudget);

        public void Write(TestContext context)
        {
            foreach ((string key, (double delta, double budget, string id, int count)) in worst)
                context.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{key,-50} n={count,3} worst {delta:G4} (budget {budget:G4}) at {id}"));
            foreach (string failure in Failures)
                context.WriteLine("FAIL " + failure);
        }
    }
}
