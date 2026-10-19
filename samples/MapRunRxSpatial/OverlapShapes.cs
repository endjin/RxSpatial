using System.Globalization;
using RxSpatial.Geo;

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
    public void UpdateLens(VehiclePair pair, GeoShape lens)
    {
        List<double[][][]> polygons = PolygonsOf(lens);
        if (polygons.Count == 0)
        {
            RemoveLens(pair);
            return;
        }

        // The exact area of the curved lens, not of the drawn vertices. Shown in the OS
        // culture, like the panel's "Observe distance: 4,600 m".
        double squareKilometres = GeoOperations.Area(lens) / 1e6;
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
    public void UpdateZoneOverlap(string id, GeoShape overlap)
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
    /// An intersection result as GeoJSON MultiPolygon coordinates: one polygon with a single
    /// ring. <see cref="GeoOperations.Intersection"/> of two convex regions is one convex
    /// region — a polygon (a lens's arcs come as vertices at most 2π/128 apart), or the whole
    /// of a contained disc or rectangle — so there are never holes or several parts. An empty
    /// result has no polygons; nor has a touching-only result, a point that covers no area.
    /// </summary>
    private static List<double[][][]> PolygonsOf(GeoShape region)
        => region is GeoPolygon or GeoCircle
            ? [[MapLibreMap.Ring(GeoOperations.ToVertices(region))]]
            : [];
}
