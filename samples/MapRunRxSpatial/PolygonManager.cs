using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;

namespace MapRunRxSpatial;

/// <summary>A named demo area or line: the backend geography plus its map shape id.</summary>
internal sealed class DemoPolygon(string name, string? shapeId, SqlGeography geography)
{
    public string Name { get; } = name;

    /// <summary>Id of the visual shape on the MapLibre map (null until drawn).</summary>
    public string? ShapeId { get; set; } = shapeId;

    public SqlGeography Geography { get; } = geography;
}

/// <summary>Owns the demo's drawn areas and the RxRx connection lines.</summary>
internal sealed class PolygonManager(MapLibreMap map)
{
    private readonly Dictionary<string, DemoPolygon> polygons = [];
    private readonly Dictionary<string, DemoPolygon> lines = [];
    private int polygonCounter;
    private int lineCounter;

    public IEnumerable<DemoPolygon> Polygons => polygons.Values;

    public DemoPolygon? CreateLine(GeoPoint point1, GeoPoint point2, out GeoPoint[]? points)
    {
        points = null;

        // Only a zero-length line is degenerate; axis-aligned connection lines are valid.
        if (point1 == point2)
            return null;

        // WKT must use '.' decimals regardless of the OS locale.
        string wkt = FormattableString.Invariant(
            $"LINESTRING({point1.Longitude} {point1.Latitude},{point2.Longitude} {point2.Latitude})");
        SqlGeography line = SqlGeography.STLineFromText(new SqlChars(new SqlString(wkt)), 4326);

        points = [point1, point2];
        return new DemoPolygon(polygonCounter.ToString(), null, line);
    }

    /// <summary>
    /// Axis-aligned rectangle between two opposite corners, or null when the corners are
    /// degenerate (sharing a latitude or longitude). The ring is wound counter-clockwise,
    /// as SqlGeography's left-foot rule requires for an outer ring.
    /// </summary>
    public static SqlGeography? CreatePolygon(GeoPoint point1, GeoPoint point2)
    {
        if (point1.Latitude == point2.Latitude || point1.Longitude == point2.Longitude)
            return null;

        double south = Math.Min(point1.Latitude, point2.Latitude);
        double north = Math.Max(point1.Latitude, point2.Latitude);
        double west = Math.Min(point1.Longitude, point2.Longitude);
        double east = Math.Max(point1.Longitude, point2.Longitude);

        string wkt = FormattableString.Invariant(
            $"POLYGON(({west} {south},{east} {south},{east} {north},{west} {north},{west} {south}))");
        return SqlGeography.STPolyFromText(new SqlChars(new SqlString(wkt)), 4326);
    }

    public void RemoveLine(int lineId)
    {
        if (lines.Remove(lineId.ToString(), out DemoPolygon? line))
            map.RemoveShape(line.ShapeId!);
    }

    public int UpdateLine(int previousLineId, GeoPoint point1, GeoPoint point2, bool dashed, int strokeThickness)
    {
        RemoveLine(previousLineId);
        DemoPolygon? line = CreateLine(point1, point2, out GeoPoint[]? points);
        if (line is not null)
        {
            lineCounter++;
            line.ShapeId = $"line-{lineCounter}";
            lines.Add(lineCounter.ToString(), line);
            map.SetLine(line.ShapeId, points!, "red", strokeThickness, dashed, 0.7);
        }

        return lineCounter;
    }

    public DemoPolygon? AddPolygon(GeoPoint point1, GeoPoint point2)
    {
        SqlGeography? geography = CreatePolygon(point1, point2);
        if (geography is null)
            return null;

        string name = polygonCounter.ToString();
        string shapeId = $"area-{name}";
        DemoPolygon polygon = new(name, shapeId, geography);
        polygons.Add(name, polygon);
        polygonCounter++;

        GeoPoint[] ring =
        [
            new GeoPoint(point1.Latitude, point1.Longitude),
            new GeoPoint(point1.Latitude, point2.Longitude),
            new GeoPoint(point2.Latitude, point2.Longitude),
            new GeoPoint(point2.Latitude, point1.Longitude),
            new GeoPoint(point1.Latitude, point1.Longitude),
        ];
        map.SetPolygon(shapeId, ring, "orange", 2, 0.8, "orange", 0.4);
        return polygon;
    }

    public void ClearAllPolygons()
    {
        foreach (DemoPolygon polygon in polygons.Values)
            map.RemoveShape(polygon.ShapeId!);

        polygons.Clear();

        foreach (DemoPolygon line in lines.Values)
            map.RemoveShape(line.ShapeId!);

        lines.Clear();
    }
}
