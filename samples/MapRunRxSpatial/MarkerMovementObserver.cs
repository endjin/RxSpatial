using Microsoft.SqlServer.Types;
using RxSpatial;

namespace MapRunRxSpatial;

/// <summary>Forwards a vehicle's location pushes to its map marker.</summary>
internal sealed class MarkerMovementObserver(VehicleMarker marker) : IObserver<SqlGeography>
{
    private IDisposable? subscription;

    public void Attach(RxGeography source) => subscription = source.Subscribe(this);

    public void OnNext(SqlGeography location) => marker.MoveTo(location);

    public void OnCompleted()
    {
        subscription?.Dispose();
        marker.Remove();
    }

    public void OnError(Exception error)
    {
    }
}
