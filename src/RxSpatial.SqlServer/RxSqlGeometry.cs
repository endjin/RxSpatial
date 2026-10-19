using Microsoft.SqlServer.Types;

namespace RxSpatial.SqlServer;

/// <summary>
/// A planar moving object over SQL Server's <see cref="SqlGeometry"/> (Microsoft.SqlServer.Types,
/// Windows only): the sibling of <see cref="RxSqlGeography"/> and the SQL Server counterpart of
/// the cross-platform <see cref="RxGeometry"/>. It observes its own location updates and is observable by the
/// same continuous queries — distance, intersect and intersection — against stationary
/// geometries (<c>Rx*</c>) or other planar moving objects (<c>RxRx*</c>). Distances are
/// Euclidean, in the geometries' coordinate units. Observers are held in the spatial backend
/// selected by <see cref="RxSpatialOptions.PreferredDataStruct"/>; the grid backend covers
/// <see cref="RxSpatialOptions.PlanarGrid"/>.
/// </summary>
public sealed class RxSqlGeometry : IObserver<SqlGeometry>, IObservable<SqlGeometry>, IMovingObject<SqlGeometry>
{
    private readonly MovingObjectCore<SqlGeometry, SqlGeometryOperations> core;

    /// <summary>Creates a planar moving object configured by <see cref="RxSpatialOptions.Default"/>.</summary>
    public RxSqlGeometry()
        : this(RxSpatialOptions.Default)
    {
    }

    /// <summary>Creates a planar moving object with explicit settings.</summary>
    public RxSqlGeometry(RxSpatialOptions options) => core = new(options);

    /// <summary>The last location pushed to this moving object, or null before the first push.</summary>
    public SqlGeometry? Location => core.Location;

    /// <summary>Average notification latency in milliseconds (performance instrumentation).</summary>
    public double AverageLatency => core.AverageLatency;

    /// <summary>Location pushes buffered but not yet processed (always 0 in visualised mode).</summary>
    public int QueuedPushCount => core.QueuedPushCount;

    public void OnNext(SqlGeometry location) => core.Push(location);

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public IDisposable Subscribe(IObserver<SqlGeometry> observer) => core.Subscribe(observer);

    public IDisposable RxRxIntersect(RxSqlGeometry other, IObserver<bool> observer)
        => core.SubscribeToMoving(new RxRxIntersectObserver<SqlGeometry, SqlGeometryOperations>(other, observer), other);

    public IDisposable RxRxIntersection(RxSqlGeometry other, IObserver<SqlGeometry> observer)
        => core.SubscribeToMoving(new RxRxIntersectionObserver<SqlGeometry, SqlGeometryOperations>(other, observer), other);

    /// <summary>
    /// Continuous distance query to another planar moving object: <paramref name="observer"/>
    /// receives the Euclidean distance, in the geometries' coordinate units, on every location
    /// update of this object, or <see cref="double.NaN"/> when SQL Server cannot compute a
    /// distance, e.g. different SRIDs or an empty geometry.
    /// </summary>
    public IDisposable RxRxDistance(RxSqlGeometry other, IObserver<double> observer)
        => core.SubscribeToMoving(new RxRxDistanceObserver<SqlGeometry, SqlGeometryOperations>(other, observer), other);

    public IDisposable RxIntersect(SqlGeometry geometry, IObserver<bool> observer)
        => core.SubscribeToStationary(new RxIntersectObserver<SqlGeometry, SqlGeometryOperations>(geometry, observer), geometry);

    public IDisposable RxIntersection(SqlGeometry geometry, IObserver<SqlGeometry> observer)
        => core.SubscribeToStationary(new RxIntersectionObserver<SqlGeometry, SqlGeometryOperations>(geometry, observer), geometry);

    /// <summary>
    /// Continuous distance query to a stationary geometry: <paramref name="observer"/>
    /// receives the Euclidean distance, in the geometries' coordinate units, on every location
    /// update of this object, or <see cref="double.NaN"/> when SQL Server cannot compute a
    /// distance, e.g. different SRIDs or an empty geometry.
    /// </summary>
    public IDisposable RxDistance(SqlGeometry geometry, IObserver<double> observer)
        => core.SubscribeToStationary(new RxDistanceObserver<SqlGeometry, SqlGeometryOperations>(geometry, observer), geometry);
}
