namespace MapRunRxSpatial;

/// <summary>
/// Overlap tab (<c>RxRxIntersect</c>): Green while the two vehicles' reaches overlap, Gray while
/// they are watched but apart. The pair could car pool while its reaches overlap.
/// </summary>
internal sealed class VehiclePairOverlapObserver(VehicleMarker marker) : VehicleObserverBase<bool>(marker)
{
    public override void OnNext(bool overlaps)
        => Marker.ApplyColor(overlaps ? VehicleColor.Green : VehicleColor.Gray);
}
