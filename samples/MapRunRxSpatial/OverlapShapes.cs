using System.Globalization;
using Microsoft.SqlServer.Types;

namespace MapRunRxSpatial;

/// <summary>The query-result regions the Overlap tab draws (OVERLAY_STYLES in map.html).</summary>
internal enum OverlayKind
{
    /// <summary>Where two watched vehicles' reaches overlap (<c>RxRxIntersection</c>).</summary>
    Lens,

    /// <summary>The part of a vehicle's reach inside a zone (<c>RxIntersection</c>).</summary>
    ZoneOverlap,
}

/// <summary>
/// The Overlap tab's result regions on the map: one lens per watched vehicle pair (the
/// <c>RxRxIntersection</c> of their reaches, labelled with its area) and one shaded region per
/// vehicle and zone (the <c>RxIntersection</c> of its reach with the zone). Both are upserts,
/// like <see cref="ConnectionLines"/>: an update redraws the shape in place, and an empty
/// result removes it.
/// </summary>
internal sealed class OverlapShapes(MapLibreMap map)
{
    private readonly HashSet<VehiclePair> lenses = [];
    private readonly HashSet<string> zoneOverlaps = [];

    /// <summary>Draws (or, when the reaches no longer overlap, removes) the pair's lens.</summary>
    public void UpdateLens(VehiclePair pair, SqlGeography lens)
    {
        List<double[][][]> polygons = PolygonsOf(lens);
        if (polygons.Count == 0)
        {
            RemoveLens(pair);
            return;
        }

        // Shown in the OS culture, like the panel's "Observe distance: 4,600 m".
        double squareKilometres = lens.STArea().Value / 1e6;
        string label = squareKilometres.ToString("N2", CultureInfo.CurrentCulture) + " km²";

        lenses.Add(pair);
        map.SetOverlay(OverlayKind.Lens, pair.ShapeId, polygons, label);
    }

    public void RemoveLens(VehiclePair pair)
    {
        if (lenses.Remove(pair))
            map.RemoveOverlay(OverlayKind.Lens, pair.ShapeId);
    }

    /// <summary>Shades (or, when it is empty, removes) the part of a vehicle's reach inside a zone.</summary>
    public void UpdateZoneOverlap(string id, SqlGeography overlap)
    {
        List<double[][][]> polygons = PolygonsOf(overlap);
        if (polygons.Count == 0)
        {
            RemoveZoneOverlap(id);
            return;
        }

        zoneOverlaps.Add(id);
        map.SetOverlay(OverlayKind.ZoneOverlap, id, polygons, label: null);
    }

    public void RemoveZoneOverlap(string id)
    {
        if (zoneOverlaps.Remove(id))
            map.RemoveOverlay(OverlayKind.ZoneOverlap, id);
    }

    public void ClearLenses()
    {
        foreach (VehiclePair pair in lenses)
            map.RemoveOverlay(OverlayKind.Lens, pair.ShapeId);

        lenses.Clear();
    }

    public void ClearZoneOverlaps()
    {
        foreach (string id in zoneOverlaps)
            map.RemoveOverlay(OverlayKind.ZoneOverlap, id);

        zoneOverlaps.Clear();
    }

    public void Clear()
    {
        ClearLenses();
        ClearZoneOverlaps();
    }

    /// <summary>
    /// The polygons of an intersection result as GeoJSON MultiPolygon coordinates
    /// ([longitude, latitude], rounded to about 0.1 m to keep the web messages small). An empty
    /// result (an empty GeometryCollection) has none; so has a touching-only result, whose
    /// points and lines cover no area.
    /// </summary>
    private static List<double[][][]> PolygonsOf(SqlGeography region)
    {
        List<double[][][]> polygons = [];
        if (region.STIsEmpty().Value)
            return polygons;

        int count = region.STNumGeometries().Value;
        for (int g = 1; g <= count; g++)
        {
            SqlGeography part = region.STGeometryN(g);
            if (part.STGeometryType().Value != "Polygon")
                continue;

            int ringCount = part.NumRings().Value;
            double[][][] rings = new double[ringCount][][];
            for (int r = 1; r <= ringCount; r++)
            {
                SqlGeography ring = part.RingN(r);
                int pointCount = ring.STNumPoints().Value;
                double[][] points = new double[pointCount][];
                for (int p = 1; p <= pointCount; p++)
                {
                    SqlGeography point = ring.STPointN(p);
                    points[p - 1] = [Math.Round(point.Long.Value, 6), Math.Round(point.Lat.Value, 6)];
                }

                rings[r - 1] = points;
            }

            polygons.Add(rings);
        }

        return polygons;
    }
}
