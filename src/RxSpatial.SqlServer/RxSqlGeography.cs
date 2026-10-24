// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using Microsoft.SqlServer.Types;

namespace RxSpatial.SqlServer;

/// <summary>
/// A moving object over SQL Server's <see cref="SqlGeography"/> (Microsoft.SqlServer.Types,
/// Windows only): it observes its own location updates (<see cref="IObserver{T}"/>) and is
/// observable by continuous spatial queries — distance, intersect and intersection — against
/// stationary geographies (<c>Rx*</c>) or other moving objects (<c>RxRx*</c>). Observers are
/// held in the spatial backend selected by <see cref="RxSpatialOptions.PreferredDataStruct"/>.
/// It runs on the same engine as the cross-platform <see cref="RxGeography"/>; only the shape
/// maths differ.
/// </summary>
public class RxSqlGeography : IObserver<SqlGeography>, IObservable<SqlGeography>, IMovingObject<SqlGeography>
{
    private readonly MovingObjectCore<SqlGeography, SqlGeographyOperations> core;

    /// <summary>Creates a moving object configured by <see cref="RxSpatialOptions.Default"/>.</summary>
    public RxSqlGeography()
        : this(RxSpatialOptions.Default)
    {
    }

    /// <summary>Creates a moving object with explicit settings.</summary>
    public RxSqlGeography(RxSpatialOptions options) => core = new(options);

    /// <summary>The last location pushed to this moving object, or null before the first push.</summary>
    public SqlGeography? Location => core.Location;

    /// <summary>Average notification latency in milliseconds (performance instrumentation).</summary>
    public double AverageLatency => core.AverageLatency;

    /// <summary>Location pushes buffered but not yet processed (always 0 in visualised mode).</summary>
    public int QueuedPushCount => core.QueuedPushCount;

    public virtual void OnNext(SqlGeography location) => core.Push(location);

    public virtual void OnCompleted()
    {
    }

    public virtual void OnError(Exception error)
    {
    }

    public IDisposable Subscribe(IObserver<SqlGeography> observer) => core.Subscribe(observer);

    public IDisposable RxRxIntersect(RxSqlGeography other, IObserver<bool> observer)
        => core.SubscribeToMoving(new RxRxIntersectObserver<SqlGeography, SqlGeographyOperations>(other, observer), other);

    public IDisposable RxRxIntersection(RxSqlGeography other, IObserver<SqlGeography> observer)
        => core.SubscribeToMoving(new RxRxIntersectionObserver<SqlGeography, SqlGeographyOperations>(other, observer), other);

    /// <summary>
    /// Continuous distance query to another moving object: <paramref name="observer"/> receives
    /// the distance in metres on every location update of this object, or
    /// <see cref="double.NaN"/> when SQL Server cannot compute a distance, e.g. different SRIDs
    /// or an empty geography.
    /// </summary>
    public IDisposable RxRxDistance(RxSqlGeography other, IObserver<double> observer)
        => core.SubscribeToMoving(new RxRxDistanceObserver<SqlGeography, SqlGeographyOperations>(other, observer), other);

    public IDisposable RxIntersect(SqlGeography geography, IObserver<bool> observer)
        => core.SubscribeToStationary(new RxIntersectObserver<SqlGeography, SqlGeographyOperations>(geography, observer), geography);

    public IDisposable RxIntersection(SqlGeography geography, IObserver<SqlGeography> observer)
        => core.SubscribeToStationary(new RxIntersectionObserver<SqlGeography, SqlGeographyOperations>(geography, observer), geography);

    /// <summary>
    /// Continuous distance query to a stationary geography: <paramref name="observer"/>
    /// receives the distance in metres on every location update of this object, or
    /// <see cref="double.NaN"/> when SQL Server cannot compute a distance, e.g. different SRIDs
    /// or an empty geography.
    /// </summary>
    public IDisposable RxDistance(SqlGeography geography, IObserver<double> observer)
        => core.SubscribeToStationary(new RxDistanceObserver<SqlGeography, SqlGeographyOperations>(geography, observer), geography);
}
