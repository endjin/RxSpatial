using Microsoft.SqlServer.Types;
using RxSpatial;
using RxSpatial.SqlServer;

namespace MapRunRxSpatial;

/// <summary>
/// Forwards a vehicle's location pushes to its map marker. The marker goes to the push's
/// <see cref="VehicleFix"/>, supplied by the vehicle: on the Overlap tab the pushed location is
/// the reach polygon, and the marker must stay at the vehicle's own position.
/// </summary>
internal sealed class MarkerMovementObserver(VehicleMarker marker, Func<VehicleFix> currentFix) : IObserver<SqlGeography>
{
    private IDisposable? subscription;

    public void Attach(RxSqlGeography source) => subscription = source.Subscribe(this);

    public void OnNext(SqlGeography location) => marker.MoveTo(currentFix());

    public void OnCompleted()
    {
        subscription?.Dispose();
        marker.Remove();
    }

    public void OnError(Exception error)
    {
    }
}
