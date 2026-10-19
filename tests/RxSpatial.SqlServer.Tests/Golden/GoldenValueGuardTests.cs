using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Tests.Golden;
using Shouldly;

namespace RxSpatial.SqlServer.Tests.Golden;

/// <summary>
/// Guards the committed golden files against the SQL Server they claim to record: every case
/// in <c>tests/golden/*.json</c> is evaluated again with Microsoft.SqlServer.Types and must
/// match. The cross-platform fidelity tests trust these files on every operating system, so a
/// stale or hand-edited value would silently move the budget they check against.
/// </summary>
[TestClass]
[TestCategory("SqlServer")]
[OSCondition(OperatingSystems.Windows)]
public class GoldenValueGuardTests
{
    // SQL Server is deterministic for a given version; the tolerance only absorbs the last
    // bits of a JSON round trip and floating-point noise between processor generations.
    private const double RelativeTolerance = 1e-9;
    private const double AbsoluteTolerance = 1e-9;

    [TestMethod]
    public void GeodeticGoldenFile_MatchesLiveSqlServer()
        => AssertMatches(GoldenFile.Load("geodetic.json"), SqlGolden.Geodetic);

    [TestMethod]
    public void PlanarGoldenFile_MatchesLiveSqlServer()
        => AssertMatches(GoldenFile.Load("planar.json"), SqlGolden.Planar);

    [TestMethod]
    public void GoldenFiles_RecordTheSqlServerVersionInUse()
    {
        GoldenFile.Load("geodetic.json").Source.ShouldStartWith(SqlGolden.Source + " ");
        GoldenFile.Load("planar.json").Source.ShouldStartWith(SqlGolden.Source + " ");
    }

    private static void AssertMatches(GoldenFile file, Func<GoldenCases.Input, GoldenCase> evaluate)
    {
        file.Cases.ShouldNotBeEmpty();
        foreach (GoldenCase recorded in file.Cases)
        {
            GoldenSqlResult live = evaluate(SqlGolden.InputOf(recorded)).Sql;
            GoldenSqlResult golden = recorded.Sql;

            ShouldBeClose(live.Distance, golden.Distance, recorded.Id, "distance");
            ShouldBeClose(live.IntersectionArea, golden.IntersectionArea, recorded.Id, "intersectionArea");
            ShouldBeClose(live.Area, golden.Area, recorded.Id, "area");
            live.Intersects.ShouldBe(golden.Intersects, $"{recorded.Id} intersects");
            live.IntersectionType.ShouldBe(golden.IntersectionType, $"{recorded.Id} intersectionType");
        }
    }

    private static void ShouldBeClose(double? live, double? golden, string id, string field)
    {
        if (golden is null)
        {
            live.ShouldBeNull($"{id} {field}");
            return;
        }

        live.ShouldNotBeNull($"{id} {field}");
        double tolerance = Math.Max(AbsoluteTolerance, RelativeTolerance * Math.Abs(golden.Value));
        live.Value.ShouldBe(golden.Value, tolerance, $"{id} {field}");
    }
}
