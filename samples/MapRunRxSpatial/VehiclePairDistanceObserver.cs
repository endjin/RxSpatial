namespace MapRunRxSpatial;

/// <summary>
/// Colours a vehicle by its distance band to another moving vehicle: Gray while watched but
/// out of range, Red below the minimum safe distance.
/// </summary>
internal sealed class VehiclePairDistanceObserver(VehicleMarker marker) : VehicleObserverBase<double>(marker)
{
    public override void OnNext(double distance)
        => Marker.ApplyColor(DistanceBands.ToColor(
            DistanceBands.ForPair(distance, DemoParameters.ObserveDistance), VehicleColor.Gray));
}
