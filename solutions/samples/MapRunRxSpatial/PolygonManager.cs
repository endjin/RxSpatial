// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using System.Globalization;
using RxSpatial.Geo;

namespace MapRunRxSpatial;

/// <summary>A named demo area: the backend shape, its map shape id and what it means.</summary>
internal sealed class DemoPolygon(string name, string shapeId, GeoPolygon geography, AreaKind kind)
{
    public string Name { get; } = name;

    public AreaKind Kind { get; } = kind;

    /// <summary>Id of the visual shape on the MapLibre map.</summary>
    public string ShapeId { get; } = shapeId;

    public GeoPolygon Geography { get; } = geography;
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
    /// Axis-aligned rectangle between two opposite corners, or null when RxSpatial.Geo cannot
    /// represent it: degenerate corners (sharing a latitude or longitude), or a rectangle wider
    /// than <see cref="GeoShape.MaxExtent"/> (500 km), which only a far zoomed-out drag makes.
    /// </summary>
    public static GeoPolygon? CreatePolygon(GeoPoint point1, GeoPoint point2)
    {
        if (point1.Latitude == point2.Latitude || point1.Longitude == point2.Longitude)
            return null;

        try
        {
            return GeoPolygon.Rectangle(
                south: Math.Min(point1.Latitude, point2.Latitude),
                west: Math.Min(point1.Longitude, point2.Longitude),
                north: Math.Max(point1.Latitude, point2.Latitude),
                east: Math.Max(point1.Longitude, point2.Longitude));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public DemoPolygon? AddPolygon(GeoPoint point1, GeoPoint point2, AreaKind kind)
    {
        GeoPolygon? geography = CreatePolygon(point1, point2);
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
