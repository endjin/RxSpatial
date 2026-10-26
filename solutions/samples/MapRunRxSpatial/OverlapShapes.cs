// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

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
    // Each drawn lens's shape id, kept rather than formatted again on every update.
    private readonly Dictionary<VehiclePair, string> lenses = [];
    private readonly HashSet<string> zoneOverlaps = [];

    /// <summary>Draws (or, when the reaches no longer overlap, removes) the pair's lens.</summary>
    public void UpdateLens(VehiclePair pair, GeoShape lens)
    {
        if (!HasArea(lens))
        {
            RemoveLens(pair);
            return;
        }

        // The exact area of the curved lens, not of the drawn vertices. Shown in the OS
        // culture, like the panel's "Observe distance: 4,600 m".
        double squareKilometres = GeoOperations.Area(lens) / 1e6;
        string label = squareKilometres.ToString("N2", CultureInfo.CurrentCulture) + " km²";

        if (!lenses.TryGetValue(pair, out string? shapeId))
        {
            shapeId = pair.ShapeId;
            lenses.Add(pair, shapeId);
        }

        map.SetOverlay(OverlayKind.Lens, shapeId, lens, label);
    }

    public void RemoveLens(VehiclePair pair)
    {
        if (lenses.Remove(pair, out string? shapeId))
            map.RemoveOverlay(OverlayKind.Lens, shapeId);
    }

    /// <summary>Shades (or, when it is empty, removes) the part of a vehicle's reach inside a zone.</summary>
    public void UpdateZoneOverlap(string id, GeoShape overlap)
    {
        if (!HasArea(overlap))
        {
            RemoveZoneOverlap(id);
            return;
        }

        zoneOverlaps.Add(id);
        map.SetOverlay(OverlayKind.ZoneOverlap, id, overlap, label: null);
    }

    public void RemoveZoneOverlap(string id)
    {
        if (zoneOverlaps.Remove(id))
            map.RemoveOverlay(OverlayKind.ZoneOverlap, id);
    }

    public void ClearLenses()
    {
        foreach (string shapeId in lenses.Values)
            map.RemoveOverlay(OverlayKind.Lens, shapeId);

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
    /// Whether an intersection result covers an area, so the map draws it as one ring.
    /// <see cref="GeoOperations.Intersection"/> of two convex regions is one convex region — a
    /// polygon (a lens's arcs come as vertices at most 2π/128 apart), or the whole of a
    /// contained disc or rectangle — so there are never holes or several parts. An empty
    /// result covers none; nor does a touching-only result, a point.
    /// </summary>
    private static bool HasArea(GeoShape region) => region is GeoPolygon or GeoCircle;
}
