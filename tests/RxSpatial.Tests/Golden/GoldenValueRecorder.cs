using System.Data.SqlTypes;
using System.Diagnostics;
using Microsoft.SqlServer.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.FormattableString;

namespace RxSpatial.Tests.Golden;

/// <summary>
/// Records SQL Server's results for <see cref="GoldenCases"/> into <c>tests/golden/geodetic.json</c>
/// and <c>tests/golden/planar.json</c>. It writes into the source tree, so it runs only when
/// <c>RXSPATIAL_RECORD_GOLDEN=1</c>; otherwise it is reported as skipped.
/// </summary>
/// <remarks>
/// Needs Windows: Microsoft.SqlServer.Types' spatial methods call a native library that ships
/// only for Windows.
/// </remarks>
[TestClass]
public class GoldenValueRecorder
{
    private const string Notes =
        "Golden values recorded from SQL Server by tests/RxSpatial.Tests/Golden/GoldenValueRecorder.cs " +
        "(set RXSPATIAL_RECORD_GOLDEN=1 and run the tests to re-record). Schema: see GoldenFile.cs. " +
        "Circles are Point.STBuffer(radius): SQL Server approximates them with a 128-vertex polygon.";

    [TestMethod]
    [EnvironmentVariableCondition("RXSPATIAL_RECORD_GOLDEN", "1")]
    public void RecordGoldenValues()
    {
        string directory = Path.Combine(RepositoryRoot(), "tests", "golden");
        Directory.CreateDirectory(directory);
        string source = "Microsoft.SqlServer.Types " +
            FileVersionInfo.GetVersionInfo(typeof(SqlGeography).Assembly.Location).ProductVersion;

        GoldenFile geodetic = new(
            GoldenFile.SchemaName,
            "geodetic",
            source + " SqlGeography, SRID 4326; metres and square metres",
            Notes,
            [.. GoldenCases.Geodetic().Select(c => Record(c, ToSqlGeography))]);
        File.WriteAllText(Path.Combine(directory, "geodetic.json"), geodetic.ToJson());

        GoldenFile planar = new(
            GoldenFile.SchemaName,
            "planar",
            source + " SqlGeometry, SRID 0; coordinate units",
            Notes,
            [.. GoldenCases.Planar().Select(c => Record(c, ToSqlGeometry))]);
        File.WriteAllText(Path.Combine(directory, "planar.json"), planar.ToJson());
    }

    private static GoldenCase Record(GoldenCases.Input input, Func<GoldenShape, ISqlShape> convert)
    {
        ISqlShape a = convert(input.A);
        GoldenSqlResult result;
        if (input.B is null)
        {
            result = new() { Area = a.Area() };
        }
        else
        {
            ISqlShape b = convert(input.B);
            ISqlShape intersection = a.Intersection(b);
            result = new()
            {
                Distance = a.Distance(b),
                Intersects = a.Intersects(b),
                IntersectionArea = intersection.Area(),
                IntersectionType = intersection.TypeName(),
            };
        }

        return new(input.Id, input.Category, input.A, input.B, input.Clearance, result);
    }

    private static SqlGeographyShape ToSqlGeography(GoldenShape shape) => shape.Type switch
    {
        "point" => new(SqlGeography.Point(shape.Lat!.Value, shape.Lon!.Value, 4326)),
        "circle" => new(SqlGeography.Point(shape.Lat!.Value, shape.Lon!.Value, 4326).STBuffer(shape.Radius!.Value)),
        "rectangle" => new(SqlGeography.STPolyFromText(
            Wkt(shape.West!.Value, shape.South!.Value, shape.East!.Value, shape.North!.Value), 4326)),
        _ => throw new ArgumentException($"Unknown shape type {shape.Type}.", nameof(shape)),
    };

    private static SqlGeometryShape ToSqlGeometry(GoldenShape shape) => shape.Type switch
    {
        "point" => new(SqlGeometry.Point(shape.X!.Value, shape.Y!.Value, 0)),
        "circle" => new(SqlGeometry.Point(shape.X!.Value, shape.Y!.Value, 0).STBuffer(shape.Radius!.Value)),
        "rectangle" => new(SqlGeometry.STPolyFromText(
            Wkt(shape.MinX!.Value, shape.MinY!.Value, shape.MaxX!.Value, shape.MaxY!.Value), 0)),
        _ => throw new ArgumentException($"Unknown shape type {shape.Type}.", nameof(shape)),
    };

    /// <summary>A counter-clockwise rectangle (SQL Server geography's left-hand rule).</summary>
    private static SqlChars Wkt(double minX, double minY, double maxX, double maxY)
        => new(new SqlString(Invariant(
            $"POLYGON(({minX} {minY}, {maxX} {minY}, {maxX} {maxY}, {minX} {maxY}, {minX} {minY}))")));

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RxSpatial.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("RxSpatial.slnx not found above the test assembly.");
    }

    /// <summary>The SQL Server operations the recorder needs, over geography and geometry alike.</summary>
    private interface ISqlShape
    {
        double Distance(ISqlShape other);

        bool Intersects(ISqlShape other);

        ISqlShape Intersection(ISqlShape other);

        double Area();

        string TypeName();
    }

    private sealed class SqlGeographyShape(SqlGeography shape) : ISqlShape
    {
        public double Distance(ISqlShape other) => shape.STDistance(Of(other)).Value;

        public bool Intersects(ISqlShape other) => shape.STIntersects(Of(other)).IsTrue;

        public ISqlShape Intersection(ISqlShape other) => new SqlGeographyShape(shape.STIntersection(Of(other)));

        public double Area() => shape.STIsEmpty().IsTrue ? 0 : shape.STArea().Value;

        public string TypeName() => shape.STIsEmpty().IsTrue ? "Empty" : shape.STGeometryType().Value;

        private static SqlGeography Of(ISqlShape other) => ((SqlGeographyShape)other).Shape;

        private SqlGeography Shape => shape;
    }

    private sealed class SqlGeometryShape(SqlGeometry shape) : ISqlShape
    {
        public double Distance(ISqlShape other) => shape.STDistance(Of(other)).Value;

        public bool Intersects(ISqlShape other) => shape.STIntersects(Of(other)).IsTrue;

        public ISqlShape Intersection(ISqlShape other) => new SqlGeometryShape(shape.STIntersection(Of(other)));

        public double Area() => shape.STIsEmpty().IsTrue ? 0 : shape.STArea().Value;

        public string TypeName() => shape.STIsEmpty().IsTrue ? "Empty" : shape.STGeometryType().Value;

        private static SqlGeometry Of(ISqlShape other) => ((SqlGeometryShape)other).Shape;

        private SqlGeometry Shape => shape;
    }
}
