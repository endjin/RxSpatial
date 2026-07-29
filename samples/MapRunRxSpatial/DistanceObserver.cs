using System.Data.SqlTypes;

namespace MapRunRxSpatial;

/// <summary>Colours a vehicle by its distance band to a stationary area.</summary>
internal sealed class DistanceObserver(VehicleMarker marker) : VehicleObserverBase<SqlDouble>(marker)
{
    public override void OnNext(SqlDouble distance) => Marker.ApplyColor(ColorForDistance(distance.Value));

    /// <summary>The original demo's distance-band colour ladder.</summary>
    internal static string ColorForDistance(double distance)
    {
        double observeDistance = DemoParameters.ObserveDistance;
        return distance switch
        {
            0 => "red",
            _ when distance >= observeDistance => "blue",
            _ when distance < observeDistance * 4 / 7 => "orange",
            _ => "green",
        };
    }
}
