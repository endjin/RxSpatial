namespace MapRunRxSpatial;

/// <summary>
/// Colours a vehicle red while it intersects an area. Demo simplification: with several
/// areas, the most recently notified query wins the colour.
/// </summary>
internal sealed class IntersectObserver(VehicleMarker marker) : VehicleObserverBase(marker), IObserver<bool>
{
    public void OnNext(bool intersects) => Marker.ApplyColor(intersects ? "red" : "blue");

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }
}
