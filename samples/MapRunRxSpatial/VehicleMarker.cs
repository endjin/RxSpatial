using System.Globalization;
using RxSpatial.Geo;

namespace MapRunRxSpatial;

/// <summary>
/// Drives one vehicle's marker on the map, with its reach circle on the Overlap tab, and the
/// shapes its queries draw: the connection lines and lenses of its watched pairs and its zone
/// overlaps. They are removed with the vehicle.
/// </summary>
internal sealed class VehicleMarker(int id, MapLibreMap map, ConnectionLines lines, OverlapShapes overlaps)
{
    private readonly string markerId = $"obj-{id}";
    private readonly HashSet<VehiclePair> pairs = [];
    private readonly HashSet<VehiclePair> lensPairs = [];
    private readonly HashSet<string> zoneOverlapIds = [];
    private VehicleColor color = VehicleColor.Blue;
    private GeoPoint position;
    private bool visible;

    /// <summary>Redraws the pair's shared line between this vehicle and the other one.</summary>
    public void UpdateConnectionLine(VehiclePair pair, GeoPoint otherPosition, DistanceBand band)
    {
        pairs.Add(pair);
        lines.Update(pair, position, otherPosition, band);
    }

    /// <summary>Redraws (or removes, when it is empty) the pair's lens.</summary>
    public void UpdateLens(VehiclePair pair, GeoShape lens)
    {
        lensPairs.Add(pair);
        overlaps.UpdateLens(pair, lens);
    }

    /// <summary>Redraws (or removes, when it is empty) the part of this vehicle's reach inside a zone.</summary>
    public void UpdateZoneOverlap(string shapeId, GeoShape overlap)
    {
        zoneOverlapIds.Add(shapeId);
        overlaps.UpdateZoneOverlap(shapeId, overlap);
    }

    /// <summary>
    /// Raises the marker to <paramref name="candidate"/> if it is more urgent than the current
    /// colour; an equal or less urgent colour is ignored. Only <see cref="MoveTo"/> lowers the
    /// colour (back to Blue), so with several queries the most urgent one wins.
    /// </summary>
    public void ApplyColor(VehicleColor candidate)
    {
        if (candidate <= color)
            return;

        color = candidate;
        if (visible)
            map.SetMarkerColor(markerId, color.Style);
    }

    /// <summary>
    /// Moves the marker (and its reach circle, if any) and resets it to Blue. RxGeography
    /// notifies location observers before query observers, so this runs first and the queries
    /// then raise the colour again.
    /// </summary>
    public void MoveTo(VehicleFix fix)
    {
        position = fix.Centre;
        color = VehicleColor.Blue;
        visible = true;
        map.SetMarker(markerId, id.ToString(CultureInfo.InvariantCulture), position, color.Style, fix.Reach);
    }

    public void Remove()
    {
        visible = false;
        map.RemoveMarker(markerId);

        // Also remove the shapes this vehicle's queries drew, or they would linger on the map
        // after the vehicle is gone.
        foreach (VehiclePair pair in pairs)
            lines.Remove(pair);

        foreach (VehiclePair pair in lensPairs)
            overlaps.RemoveLens(pair);

        foreach (string shapeId in zoneOverlapIds)
            overlaps.RemoveZoneOverlap(shapeId);

        pairs.Clear();
        lensPairs.Clear();
        zoneOverlapIds.Clear();
    }
}
