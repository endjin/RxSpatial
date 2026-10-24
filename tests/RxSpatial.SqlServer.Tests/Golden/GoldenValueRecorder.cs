// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using RxSpatial.Tests.Golden;

namespace RxSpatial.SqlServer.Tests.Golden;

/// <summary>
/// Records SQL Server's results for <see cref="GoldenCases"/> into <c>tests/golden/geodetic.json</c>
/// and <c>tests/golden/planar.json</c>. It writes into the source tree, so it runs only when
/// <c>RXSPATIAL_RECORD_GOLDEN=1</c>; otherwise it is reported as skipped.
/// </summary>
/// <remarks>
/// Needs Windows: Microsoft.SqlServer.Types' spatial methods call a native library that ships
/// only for Windows. The cross-platform RxSpatial.Tests read the files on any operating system.
/// </remarks>
[TestClass]
[TestCategory("SqlServer")]
[OSCondition(OperatingSystems.Windows)]
public class GoldenValueRecorder
{
    private const string Notes =
        "Golden values recorded from SQL Server by tests/RxSpatial.SqlServer.Tests/Golden/GoldenValueRecorder.cs " +
        "(set RXSPATIAL_RECORD_GOLDEN=1 and run the tests to re-record). Schema: see GoldenFile.cs. " +
        "Circles are Point.STBuffer(radius): SQL Server approximates them with a 128-vertex polygon.";

    [TestMethod]
    [EnvironmentVariableCondition("RXSPATIAL_RECORD_GOLDEN", "1")]
    public void RecordGoldenValues()
    {
        string directory = Path.Combine(RepositoryRoot(), "tests", "golden");
        Directory.CreateDirectory(directory);

        GoldenFile geodetic = new(
            GoldenFile.SchemaName,
            "geodetic",
            SqlGolden.Source + " SqlGeography, SRID 4326; metres and square metres",
            Notes,
            [.. GoldenCases.Geodetic().Select(SqlGolden.Geodetic)]);
        File.WriteAllText(Path.Combine(directory, "geodetic.json"), geodetic.ToJson());

        GoldenFile planar = new(
            GoldenFile.SchemaName,
            "planar",
            SqlGolden.Source + " SqlGeometry, SRID 0; coordinate units",
            Notes,
            [.. GoldenCases.Planar().Select(SqlGolden.Planar)]);
        File.WriteAllText(Path.Combine(directory, "planar.json"), planar.ToJson());
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RxSpatial.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("RxSpatial.slnx not found above the test assembly.");
    }
}
