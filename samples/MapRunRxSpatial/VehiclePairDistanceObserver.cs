using System.Data.SqlTypes;

namespace MapRunRxSpatial;

/// <summary>
/// Colours a vehicle by its distance band to another moving vehicle: Gray while watched but
/// out of range, Red below the minimum safe distance.
/// </summary>
internal sealed class VehiclePairDistanceObserver(VehicleMarker marker) : VehicleObserverBase<SqlDouble>(marker)
{
    public override void OnNext(SqlDouble distance)
        => Marker.ApplyColor(DistanceBands.ToColor(
            DistanceBands.ForPair(distance.Value, DemoParameters.ObserveDistance), VehicleColor.Gray));
}
