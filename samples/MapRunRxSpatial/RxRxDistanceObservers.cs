using System.Data.SqlTypes;
using RxSpatial;

namespace MapRunRxSpatial;

/// <summary>Colours a vehicle by its distance band to another moving vehicle.</summary>
internal sealed class RxRxDistanceObserver(VehicleMarker marker) : VehicleObserverBase<SqlDouble>(marker)
{
    public override void OnNext(SqlDouble distance)
        => Marker.ApplyColor(DistanceObserver.ColorForDistance(distance.Value));
}

/// <summary>
/// Like <see cref="RxRxDistanceObserver"/>, but also draws a connection line to the tracked
/// vehicle whose style reflects the distance band.
/// </summary>
internal sealed class RxRxDistanceConnectionObserver(RxGeography other, VehicleMarker marker)
    : VehicleObserverBase<SqlDouble>(marker)
{
    public override void OnNext(SqlDouble distance)
    {
        double observeDistance = DemoParameters.ObserveDistance;
        if (distance.Value >= observeDistance)
        {
            Marker.ApplyColor("gray");
            Marker.UpdateConnectionLine(other, dashed: true, strokeThickness: 1);
        }
        else if (distance.Value == 0)
        {
            Marker.ApplyColor("red");
        }
        else if (distance.Value < observeDistance * 4 / 7)
        {
            Marker.ApplyColor("orange");
            Marker.UpdateConnectionLine(other, dashed: false, strokeThickness: 3);
        }
        else
        {
            Marker.ApplyColor("green");
            Marker.UpdateConnectionLine(other, dashed: false, strokeThickness: 2);
        }
    }
}
