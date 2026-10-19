using System.Data.SqlTypes;
using System.Diagnostics;
using Microsoft.SqlServer.Types;
using RxSpatial.Tests.Golden;
using static System.FormattableString;

namespace RxSpatial.SqlServer.Tests.Golden;

/// <summary>
/// Evaluates golden cases with SQL Server (Microsoft.SqlServer.Types): the one place that
/// turns a <see cref="GoldenShape"/> into a <see cref="SqlGeography"/> or
/// <see cref="SqlGeometry"/> and reads back the results the golden files record. Used by the
/// <see cref="GoldenValueRecorder"/> and the <see cref="GoldenValueGuardTests"/>.
/// </summary>
internal static class SqlGolden
{
    /// <summary>The <c>source</c> recorded in the files: the Microsoft.SqlServer.Types version.</summary>
    public static string Source
        => "Microsoft.SqlServer.Types "
           + FileVersionInfo.GetVersionInfo(typeof(SqlGeography).Assembly.Location).ProductVersion;

    /// <summary>A geodetic case (SqlGeography, SRID 4326) with SQL Server's results.</summary>
    public static GoldenCase Geodetic(GoldenCases.Input input) => Record(input, ToSqlGeography);

    /// <summary>A planar case (SqlGeometry, SRID 0) with SQL Server's results.</summary>
    public static GoldenCase Planar(GoldenCases.Input input) => Record(input, ToSqlGeometry);

    /// <summary>The inputs of a recorded case, for evaluating it again.</summary>
    public static GoldenCases.Input InputOf(GoldenCase golden)
        => new(golden.Id, golden.Category, golden.A, golden.B, golden.Clearance);

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

    /// <summary>The SQL Server operations the golden files need, over geography and geometry alike.</summary>
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
