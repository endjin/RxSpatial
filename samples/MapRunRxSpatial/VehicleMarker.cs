using Microsoft.SqlServer.Types;
using RxSpatial;

namespace MapRunRxSpatial;

/// <summary>Drives one vehicle's marker (and its optional connection line) on the map.</summary>
internal sealed class VehicleMarker(int id, MapLibreMap map, PolygonManager polygons)
{
    private readonly string markerId = $"obj-{id}";
    private string currentColor = "blue";
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
    /// Applies a colour with the original demo's precedence ladder: red beats orange beats
    /// green; gray always wins; blue (and other colours) only reset through MoveTo.
    /// </summary>
    public void ApplyColor(string color)
    {
        switch (color)
        {
            case "gray" or "red":
                SetColor(color);
                break;
            case "orange" when currentColor != "red":
                SetColor(color);
                break;
            case "greenyellow" when currentColor is not ("red" or "orange"):
                SetColor(color);
                break;
            case "green" when currentColor is not ("red" or "orange" or "greenyellow"):
                SetColor(color);
                break;
        }
    }

    public void MoveTo(SqlGeography? location)
    {
        if (location is not null)
        {
            position = new GeoPoint(location.Lat.Value, location.Long.Value);
            currentColor = "blue";
            visible = true;
            map.SetMarker(markerId, id.ToString(), position.Latitude, position.Longitude, currentColor);
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

    private void SetColor(string color)
    {
        currentColor = color;
        if (visible)
            map.SetMarkerColor(markerId, color);
    }
}
