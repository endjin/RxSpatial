// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using RxSpatial.Geo;

namespace RxSpatial;

/// <summary>
/// A planar moving object, the sibling of <see cref="RxGeography"/> named in the ICDE 2017
/// paper: it observes its own location updates and is observable by the same continuous
/// queries — distance, intersect and intersection — against stationary shapes (<c>Rx*</c>) or
/// other planar moving objects (<c>RxRx*</c>). Locations and zones are
/// <see cref="PlanarShape"/>s computed by the exact Euclidean <see cref="PlanarOperations"/>;
/// distances are in the shapes' coordinate units. Observers are held in the spatial backend
/// selected by <see cref="RxSpatialOptions.PreferredDataStruct"/>; the grid backend covers
/// <see cref="RxSpatialOptions.PlanarGrid"/>.
/// </summary>
/// <remarks>
/// The grid and tree backends index a shape by its bounds, which <see cref="PlanarEmpty"/>
/// does not have: pushing or subscribing an empty shape on those backends throws
/// <see cref="InvalidOperationException"/>. The list backend accepts it, and its distance is
/// <see cref="double.NaN"/>.
/// </remarks>
public sealed class RxGeometry : IObserver<PlanarShape>, IObservable<PlanarShape>, IMovingObject<PlanarShape>
{
    private readonly MovingObjectCore<PlanarShape, GeometryOperations> core;

    /// <summary>Creates a planar moving object configured by <see cref="RxSpatialOptions.Default"/>.</summary>
    public RxGeometry()
        : this(RxSpatialOptions.Default)
    {
    }

    /// <summary>Creates a planar moving object with explicit settings.</summary>
    public RxGeometry(RxSpatialOptions options) => core = new(options);

    /// <summary>The last location pushed to this moving object, or null before the first push.</summary>
    public PlanarShape? Location => core.Location;

    /// <summary>Average notification latency in milliseconds (performance instrumentation).</summary>
    public double AverageLatency => core.AverageLatency;

    /// <summary>Location pushes buffered but not yet processed (always 0 in visualised mode).</summary>
    public int QueuedPushCount => core.QueuedPushCount;

    public void OnNext(PlanarShape location) => core.Push(location);

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public IDisposable Subscribe(IObserver<PlanarShape> observer) => core.Subscribe(observer);

    public IDisposable RxRxIntersect(RxGeometry other, IObserver<bool> observer)
        => core.SubscribeToMoving(new RxRxIntersectObserver<PlanarShape, GeometryOperations>(other, observer), other);

    public IDisposable RxRxIntersection(RxGeometry other, IObserver<PlanarShape> observer)
        => core.SubscribeToMoving(new RxRxIntersectionObserver<PlanarShape, GeometryOperations>(other, observer), other);

    /// <summary>
    /// Continuous distance query to another planar moving object: <paramref name="observer"/>
    /// receives the Euclidean distance, in the shapes' coordinate units, on every location
    /// update of this object, or <see cref="double.NaN"/> when either shape is empty.
    /// </summary>
    public IDisposable RxRxDistance(RxGeometry other, IObserver<double> observer)
        => core.SubscribeToMoving(new RxRxDistanceObserver<PlanarShape, GeometryOperations>(other, observer), other);

    public IDisposable RxIntersect(PlanarShape geometry, IObserver<bool> observer)
        => core.SubscribeToStationary(new RxIntersectObserver<PlanarShape, GeometryOperations>(geometry, observer), geometry);

    public IDisposable RxIntersection(PlanarShape geometry, IObserver<PlanarShape> observer)
        => core.SubscribeToStationary(new RxIntersectionObserver<PlanarShape, GeometryOperations>(geometry, observer), geometry);

    /// <summary>
    /// Continuous distance query to a stationary shape: <paramref name="observer"/> receives
    /// the Euclidean distance, in the shapes' coordinate units, on every location update of
    /// this object, or <see cref="double.NaN"/> when either shape is empty.
    /// </summary>
    public IDisposable RxDistance(PlanarShape geometry, IObserver<double> observer)
        => core.SubscribeToStationary(new RxDistanceObserver<PlanarShape, GeometryOperations>(geometry, observer), geometry);
}
