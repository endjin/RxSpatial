using System.Text.Json;
using System.Text.Json.Serialization;

namespace RxSpatial.Tests.Golden;

/// <summary>
/// A file of golden values: spatial results that SQL Server (Microsoft.SqlServer.Types)
/// computed for a fixed set of inputs. The files live in <c>tests/golden</c>. They let the
/// cross-platform shape maths be checked against SQL Server on any operating system, because
/// the checks read JSON rather than call SQL Server.
/// </summary>
/// <remarks>
/// <para>Schema <c>rxspatial-golden/1</c>:</para>
/// <list type="bullet">
/// <item><c>space</c>: <c>geodetic</c> (SqlGeography, SRID 4326, metres and square metres) or
/// <c>planar</c> (SqlGeometry, SRID 0, coordinate units).</item>
/// <item><c>cases[]</c>: each case has an <c>id</c>, a <c>category</c>, a shape <c>a</c>, an
/// optional shape <c>b</c>, an optional <c>clearance</c> and the SQL Server results in
/// <c>sql</c>.</item>
/// <item>A shape has a <c>type</c> of <c>point</c>, <c>circle</c> or <c>rectangle</c>.
/// Geodetic shapes use <c>lat</c>/<c>lon</c> (and <c>south</c>/<c>west</c>/<c>north</c>/<c>east</c>
/// for rectangles); planar shapes use <c>x</c>/<c>y</c> (and <c>minX</c>/<c>minY</c>/<c>maxX</c>/<c>maxY</c>).
/// A circle has a <c>radius</c>; SQL Server builds it as <c>Point.STBuffer(radius)</c>.
/// A rectangle is a counter-clockwise four-vertex polygon whose edges are geodesics (geodetic)
/// or straight lines (planar).</item>
/// <item><c>clearance</c>: the signed distance from <c>b</c> to the boundary of <c>a</c> for the
/// exact (unbuffered) shapes, computed with GeographicLib. It is negative inside and absent
/// when it is not known. Intersects is only compared for cases more than 0.5 m from a boundary.</item>
/// <item><c>sql</c>: <c>distance</c> (<c>STDistance</c>), <c>intersects</c> (<c>STIntersects</c>),
/// <c>intersectionArea</c> (<c>STIntersection().STArea()</c>) and
/// <c>intersectionType</c> for pairs; <c>area</c> (<c>STArea</c>) for single shapes.</item>
/// </list>
/// <para>The files carry no timestamp, so re-recording with the same SQL Server version
/// reproduces them byte for byte.</para>
/// </remarks>
internal sealed record GoldenFile(
    string Schema,
    string Space,
    string Source,
    string Notes,
    IReadOnlyList<GoldenCase> Cases)
{
    public const string SchemaName = "rxspatial-golden/1";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
    };

    /// <summary>Reads a golden file copied next to the test assembly (<c>golden/*.json</c>).</summary>
    public static GoldenFile Load(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "golden", fileName);
        GoldenFile? file = JsonSerializer.Deserialize<GoldenFile>(File.ReadAllText(path), JsonOptions);
        return file is { Schema: SchemaName }
            ? file
            : throw new InvalidDataException($"{path} is not a {SchemaName} file.");
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions) + "\n";
}

/// <summary>One golden case: one or two input shapes and what SQL Server computed for them.</summary>
internal sealed record GoldenCase(
    string Id,
    string Category,
    GoldenShape A,
    GoldenShape? B,
    double? Clearance,
    GoldenSqlResult Sql);

/// <summary>An input shape. Only the fields for its <see cref="Type"/> and space are set.</summary>
internal sealed record GoldenShape(string Type)
{
    public double? Lat { get; init; }

    public double? Lon { get; init; }

    public double? X { get; init; }

    public double? Y { get; init; }

    public double? Radius { get; init; }

    public double? South { get; init; }

    public double? West { get; init; }

    public double? North { get; init; }

    public double? East { get; init; }

    public double? MinX { get; init; }

    public double? MinY { get; init; }

    public double? MaxX { get; init; }

    public double? MaxY { get; init; }

    public static GoldenShape GeoPoint(double lat, double lon) => new("point") { Lat = lat, Lon = lon };

    public static GoldenShape GeoCircle(double lat, double lon, double radius)
        => new("circle") { Lat = lat, Lon = lon, Radius = radius };

    public static GoldenShape GeoRectangle(double south, double west, double north, double east)
        => new("rectangle") { South = south, West = west, North = north, East = east };

    public static GoldenShape PlanarPoint(double x, double y) => new("point") { X = x, Y = y };

    public static GoldenShape PlanarCircle(double x, double y, double radius)
        => new("circle") { X = x, Y = y, Radius = radius };

    public static GoldenShape PlanarRectangle(double minX, double minY, double maxX, double maxY)
        => new("rectangle") { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
}

/// <summary>SQL Server's results for a case. Pair cases set the first four; single-shape cases set <see cref="Area"/>.</summary>
internal sealed record GoldenSqlResult
{
    public double? Distance { get; init; }

    public bool? Intersects { get; init; }

    public double? IntersectionArea { get; init; }

    public string? IntersectionType { get; init; }

    public double? Area { get; init; }
}
