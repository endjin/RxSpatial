namespace MapRunRxSpatial;

/// <summary>
/// Colours a vehicle red while it intersects an area. Demo simplification: with several
/// areas, the most recently notified query wins the colour.
/// </summary>
internal sealed class IntersectObserver(VehicleMarker marker) : VehicleObserverBase<bool>(marker)
{
    public override void OnNext(bool intersects) => Marker.ApplyColor(intersects ? "red" : "blue");
}
