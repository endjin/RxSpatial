using System.Data.SqlTypes;

namespace MapRunRxSpatial;

/// <summary>Colours a vehicle by its distance band to a stationary area.</summary>
internal sealed class DistanceObserver(VehicleMarker marker) : VehicleObserverBase<SqlDouble>(marker)
{
    public override void OnNext(SqlDouble distance)
        => Marker.ApplyColor(DistanceBands.ToColor(
            DistanceBands.ForArea(distance.Value, DemoParameters.ObserveDistance), VehicleColor.Blue));
}
