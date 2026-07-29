namespace MapRunRxSpatial;

/// <summary>
/// Base for demo observers that colour a vehicle's marker from continuous-query results.
/// Subclasses supply only the reaction to a result.
/// </summary>
internal abstract class VehicleObserverBase<TValue>(VehicleMarker marker) : IObserver<TValue>
{
    protected VehicleMarker Marker { get; } = marker;

    public abstract void OnNext(TValue value);

    // The demo's queries run for as long as the vehicle exists; neither completion nor an
    // error on a query stream needs a visual response.
    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }
}
