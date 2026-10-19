namespace MapRunRxSpatial;

/// <summary>
/// Colours a vehicle red while it intersects an area. With several areas, red from any
/// intersecting area wins: a non-intersecting area's Blue never lowers the colour.
/// </summary>
internal sealed class IntersectObserver(VehicleMarker marker) : VehicleObserverBase<bool>(marker)
{
    public override void OnNext(bool intersects)
        => Marker.ApplyColor(intersects ? VehicleColor.Red : VehicleColor.Blue);
}
