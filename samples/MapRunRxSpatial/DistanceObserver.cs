namespace MapRunRxSpatial;

/// <summary>Colours a vehicle by its distance band to a stationary area.</summary>
internal sealed class DistanceObserver(VehicleMarker marker) : VehicleObserverBase<double>(marker)
{
    public override void OnNext(double distance)
        => Marker.ApplyColor(DistanceBands.ToColor(
            DistanceBands.ForArea(distance, DemoParameters.ObserveDistance), VehicleColor.Blue));
}
