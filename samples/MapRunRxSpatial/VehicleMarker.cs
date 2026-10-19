using System.Globalization;
using Microsoft.SqlServer.Types;

namespace MapRunRxSpatial;

/// <summary>Drives one vehicle's marker (and the connection lines of its watched pairs) on the map.</summary>
internal sealed class VehicleMarker(int id, MapLibreMap map, ConnectionLines lines)
{
    private readonly string markerId = $"obj-{id}";
    private readonly HashSet<VehiclePair> pairs = [];
    private VehicleColor color = VehicleColor.Blue;
    private GeoPoint position;
    private bool visible;

    /// <summary>Redraws the pair's shared line between this vehicle and the other one.</summary>
    public void UpdateConnectionLine(VehiclePair pair, SqlGeography otherLocation, DistanceBand band)
    {
        pairs.Add(pair);
        lines.Update(pair, position, new GeoPoint(otherLocation.Lat.Value, otherLocation.Long.Value), band);
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
    /// Moves the marker and resets it to Blue. RxGeography notifies location observers before
    /// query observers, so this runs first and the queries then raise the colour again.
    /// </summary>
    public void MoveTo(SqlGeography? location)
    {
        if (location is not null)
        {
            position = new GeoPoint(location.Lat.Value, location.Long.Value);
            color = VehicleColor.Blue;
            visible = true;
            map.SetMarker(
                markerId,
                id.ToString(CultureInfo.InvariantCulture),
                position.Latitude,
                position.Longitude,
                color.Style);
        }
        else
        {
            visible = false;
            map.RemoveMarker(markerId);
        }
    }

    public void Remove()
    {
        visible = false;
        map.RemoveMarker(markerId);

        // Also remove this vehicle's connection lines, or they would linger on the map
        // after the vehicle is gone.
        foreach (VehiclePair pair in pairs)
            lines.Remove(pair);

        pairs.Clear();
    }
}
