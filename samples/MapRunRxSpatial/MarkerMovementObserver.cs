using RxSpatial;
using RxSpatial.Geo;

namespace MapRunRxSpatial;

/// <summary>
/// Forwards a vehicle's location pushes to its map marker. The marker goes to the push's
/// <see cref="VehicleFix"/>, supplied by the vehicle: on the Overlap tab the pushed location is
/// the reach disc, and the marker must stay at the vehicle's own position.
/// </summary>
internal sealed class MarkerMovementObserver(VehicleMarker marker, Func<VehicleFix> currentFix) : IObserver<GeoShape>
{
    private IDisposable? subscription;

    public void Attach(RxGeography source) => subscription = source.Subscribe(this);

    public void OnNext(GeoShape location) => marker.MoveTo(currentFix());

    public void OnCompleted()
    {
        subscription?.Dispose();
        marker.Remove();
    }

    public void OnError(Exception error)
    {
    }
}
