using System.Data.SqlTypes;
using RxSpatial;

namespace MapRunRxSpatial;

/// <summary>
/// Colours a vehicle by its distance band to another moving vehicle: Gray while watched but
/// out of range, Red below the minimum safe distance.
/// </summary>
internal sealed class RxRxDistanceObserver(VehicleMarker marker) : VehicleObserverBase<SqlDouble>(marker)
{
    public override void OnNext(SqlDouble distance)
        => Marker.ApplyColor(DistanceBands.ToColor(
            DistanceBands.ForPair(distance.Value, DemoParameters.ObserveDistance), VehicleColor.Gray));
}

/// <summary>
/// Like <see cref="RxRxDistanceObserver"/>, but also draws a connection line to the tracked
/// vehicle whose style reflects the distance band. The line is redrawn in every band, so it
/// never goes stale.
/// </summary>
internal sealed class RxRxDistanceConnectionObserver(RxGeography other, VehicleMarker marker)
    : VehicleObserverBase<SqlDouble>(marker)
{
    public override void OnNext(SqlDouble distance)
    {
        DistanceBand band = DistanceBands.ForPair(distance.Value, DemoParameters.ObserveDistance);
        Marker.ApplyColor(DistanceBands.ToColor(band, VehicleColor.Gray));
        switch (band)
        {
            case DistanceBand.TooClose:
                Marker.UpdateConnectionLine(other, dashed: false, strokeThickness: 5);
                break;
            case DistanceBand.Stable:
                Marker.UpdateConnectionLine(other, dashed: false, strokeThickness: 4);
                break;
            case DistanceBand.NearLimit:
                Marker.UpdateConnectionLine(other, dashed: false, strokeThickness: 2);
                break;
            default:
                Marker.UpdateConnectionLine(other, dashed: true, strokeThickness: 1);
                break;
        }
    }
}
