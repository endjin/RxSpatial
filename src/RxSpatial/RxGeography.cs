using RxSpatial.Geo;

namespace RxSpatial;

/// <summary>
/// A moving object on the WGS84 ellipsoid: it observes its own location updates
/// (<see cref="IObserver{T}"/>) and is observable by continuous spatial queries — distance,
/// intersect and intersection — against stationary shapes (<c>Rx*</c>) or other moving objects
/// (<c>RxRx*</c>). Locations and zones are <see cref="GeoShape"/>s (points, geodesic circles,
/// convex polygons) computed by the cross-platform <see cref="GeoOperations"/>. Observers are
/// held in the spatial backend selected by <see cref="RxSpatialOptions.PreferredDataStruct"/>.
/// </summary>
/// <remarks>
/// The grid and tree backends index a shape by its bounds, which <see cref="GeoEmpty"/> does
/// not have: pushing or subscribing an empty shape on those backends throws
/// <see cref="InvalidOperationException"/>, as an empty SQL Server geography did. The list
/// backend accepts it, and its distance is <see cref="double.NaN"/>.
/// </remarks>
public class RxGeography : IObserver<GeoShape>, IObservable<GeoShape>, IMovingObject<GeoShape>
{
    private readonly MovingObjectCore<GeoShape, GeographyOperations> core;

    /// <summary>Creates a moving object configured by <see cref="RxSpatialOptions.Default"/>.</summary>
    public RxGeography()
        : this(RxSpatialOptions.Default)
    {
    }

    /// <summary>Creates a moving object with explicit settings.</summary>
    public RxGeography(RxSpatialOptions options) => core = new(options);

    /// <summary>The last location pushed to this moving object, or null before the first push.</summary>
    public GeoShape? Location => core.Location;

    /// <summary>Average notification latency in milliseconds (performance instrumentation).</summary>
    public double AverageLatency => core.AverageLatency;

    /// <summary>Location pushes buffered but not yet processed (always 0 in visualised mode).</summary>
    public int QueuedPushCount => core.QueuedPushCount;

    public virtual void OnNext(GeoShape location) => core.Push(location);

    public virtual void OnCompleted()
    {
    }

    public virtual void OnError(Exception error)
    {
    }

    public IDisposable Subscribe(IObserver<GeoShape> observer) => core.Subscribe(observer);

    public IDisposable RxRxIntersect(RxGeography other, IObserver<bool> observer)
        => core.SubscribeToMoving(new RxRxIntersectObserver<GeoShape, GeographyOperations>(other, observer), other);

    public IDisposable RxRxIntersection(RxGeography other, IObserver<GeoShape> observer)
        => core.SubscribeToMoving(new RxRxIntersectionObserver<GeoShape, GeographyOperations>(other, observer), other);

    /// <summary>
    /// Continuous distance query to another moving object: <paramref name="observer"/> receives
    /// the geodesic distance in metres on every location update of this object, or
    /// <see cref="double.NaN"/> when there is none: an empty shape, or a nearly antipodal pair
    /// whose geodesic does not converge.
    /// </summary>
    public IDisposable RxRxDistance(RxGeography other, IObserver<double> observer)
        => core.SubscribeToMoving(new RxRxDistanceObserver<GeoShape, GeographyOperations>(other, observer), other);

    public IDisposable RxIntersect(GeoShape geography, IObserver<bool> observer)
        => core.SubscribeToStationary(new RxIntersectObserver<GeoShape, GeographyOperations>(geography, observer), geography);

    public IDisposable RxIntersection(GeoShape geography, IObserver<GeoShape> observer)
        => core.SubscribeToStationary(new RxIntersectionObserver<GeoShape, GeographyOperations>(geography, observer), geography);

    /// <summary>
    /// Continuous distance query to a stationary shape: <paramref name="observer"/> receives
    /// the geodesic distance in metres on every location update of this object, or
    /// <see cref="double.NaN"/> when there is none: an empty shape, or a nearly antipodal pair
    /// whose geodesic does not converge.
    /// </summary>
    public IDisposable RxDistance(GeoShape geography, IObserver<double> observer)
        => core.SubscribeToStationary(new RxDistanceObserver<GeoShape, GeographyOperations>(geography, observer), geography);
}
