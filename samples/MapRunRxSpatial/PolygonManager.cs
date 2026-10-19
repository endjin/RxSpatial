using System.Data.SqlTypes;
using System.Globalization;
using Microsoft.SqlServer.Types;

namespace MapRunRxSpatial;

/// <summary>A named demo area: the backend geography, its map shape id and what it means.</summary>
internal sealed class DemoPolygon(string name, string shapeId, SqlGeography geography, AreaKind kind)
{
    public string Name { get; } = name;

    public AreaKind Kind { get; } = kind;

    /// <summary>Id of the visual shape on the MapLibre map.</summary>
    public string ShapeId { get; } = shapeId;

    public SqlGeography Geography { get; } = geography;
}

/// <summary>Owns the demo's drawn areas (connection lines live in <see cref="ConnectionLines"/>).</summary>
internal sealed class PolygonManager(MapLibreMap map)
{
    private readonly Dictionary<string, DemoPolygon> polygons = [];
    private int polygonCounter;

    public IEnumerable<DemoPolygon> Polygons => polygons.Values;

    /// <summary>Dictionary keys are formed from ids, so they must not follow the OS locale.</summary>
    private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);

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

    public DemoPolygon? AddPolygon(GeoPoint point1, GeoPoint point2, AreaKind kind)
    {
        SqlGeography? geography = CreatePolygon(point1, point2);
        if (geography is null)
            return null;

        string name = Key(polygonCounter);
        string shapeId = $"area-{name}";
        DemoPolygon polygon = new(name, shapeId, geography, kind);
        polygons.Add(name, polygon);
        polygonCounter++;

        // The map styles the area by its kind (see AREA_STYLES in map.html).
        map.SetArea(shapeId, kind, point1, point2);
        return polygon;
    }

    public void ClearAllPolygons()
    {
        foreach (DemoPolygon polygon in polygons.Values)
            map.RemoveShape(polygon.ShapeId);

        polygons.Clear();
    }
}
