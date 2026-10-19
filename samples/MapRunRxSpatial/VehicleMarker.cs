using System.Globalization;
using Microsoft.SqlServer.Types;
using RxSpatial;

namespace MapRunRxSpatial;

/// <summary>Drives one vehicle's marker (and its optional connection line) on the map.</summary>
internal sealed class VehicleMarker(int id, MapLibreMap map, PolygonManager polygons)
{
    private readonly string markerId = $"obj-{id}";
    private VehicleColor color = VehicleColor.Blue;
    private GeoPoint position;
    private bool visible;
    private int lineId = -1;

    public void UpdateConnectionLine(RxGeography other, bool dashed, int strokeThickness)
    {
        SqlGeography otherLocation = other.Location!;
        lineId = polygons.UpdateLine(
            lineId,
            new GeoPoint(otherLocation.Lat.Value, otherLocation.Long.Value),
            position,
            dashed,
            strokeThickness);
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
        if (lineId != -1)
        {
            // Also remove this vehicle's RxRx connection line, or it would linger
            // on the map after the vehicle is gone.
            polygons.RemoveLine(lineId);
            lineId = -1;
        }
    }
}
