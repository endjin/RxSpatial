using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace RxSpatial.Tests.Golden;

/// <summary>The recorded golden files are present, readable and complete.</summary>
[TestClass]
public class GoldenFileTests
{
    [TestMethod]
    [DataRow("geodetic.json", "geodetic")]
    [DataRow("planar.json", "planar")]
    public void GoldenFile_LoadsWithAResultForEveryCase(string fileName, string space)
    {
        GoldenFile file = GoldenFile.Load(fileName);

        file.Space.ShouldBe(space);
        file.Cases.ShouldNotBeEmpty();
        file.Cases.Select(c => c.Id).ShouldBeUnique();
        foreach (GoldenCase golden in file.Cases)
        {
            if (golden.B is null)
            {
                golden.Sql.Area.ShouldNotBeNull(golden.Id);
            }
            else
            {
                golden.Sql.Distance.ShouldNotBeNull(golden.Id);
                golden.Sql.Intersects.ShouldNotBeNull(golden.Id);
                golden.Sql.IntersectionArea.ShouldNotBeNull(golden.Id);
            }
        }
    }

    [TestMethod]
    public void GoldenFiles_CoverTheRecordedCaseList()
    {
        GoldenFile.Load("geodetic.json").Cases.Select(c => c.Id)
                  .ShouldBe(GoldenCases.Geodetic().Select(c => c.Id));
        GoldenFile.Load("planar.json").Cases.Select(c => c.Id)
                  .ShouldBe(GoldenCases.Planar().Select(c => c.Id));
    }
}
