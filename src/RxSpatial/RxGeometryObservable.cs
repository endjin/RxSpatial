using System.Reactive.Linq;
using RxSpatial.Geo;

namespace RxSpatial;

/// <summary>
/// The <see cref="RxGeometry"/> counterpart of <see cref="RxGeographyObservable"/>: C# 14
/// extension members that expose the planar continuous queries as composable System.Reactive
/// (Rx.NET v7) observable sequences.
/// </summary>
public static class RxGeometryObservable
{
    extension(RxGeometry source)
    {
        /// <summary>
        /// Stream of raw location updates published by the moving object. Location updates are
        /// forwarded to direct subscribers when <see cref="RxSpatialOptions.IsVisualized"/> is
        /// <c>true</c> (the original RxSpatial behaviour).
        /// </summary>
        public IObservable<PlanarShape> Locations
            => Observable.Create<PlanarShape>(observer => source.Subscribe(observer));

        /// <summary>
        /// Stream of Euclidean distances (in the shapes' coordinate units) from the moving
        /// object to a stationary shape, re-evaluated on every location update of the moving
        /// object. A value is <see cref="double.NaN"/> when either shape is empty.
        /// </summary>
        public IObservable<double> DistanceTo(PlanarShape target)
            => Observable.Create<double>(observer => source.RxDistance(target, observer));

        /// <summary>
        /// Stream of Euclidean distances (in the shapes' coordinate units) between two moving
        /// objects, re-evaluated on every location update of the moving object. A value is
        /// <see cref="double.NaN"/> when either shape is empty.
        /// </summary>
        public IObservable<double> DistanceTo(RxGeometry target)
            => Observable.Create<double>(observer => source.RxRxDistance(target, observer));

        /// <summary>
        /// Stream of intersection tests between the moving object and a stationary shape.
        /// </summary>
        public IObservable<bool> Intersects(PlanarShape target)
            => Observable.Create<bool>(observer => source.RxIntersect(target, observer));

        /// <summary>
        /// Stream of intersection tests between two moving objects.
        /// </summary>
        public IObservable<bool> Intersects(RxGeometry target)
            => Observable.Create<bool>(observer => source.RxRxIntersect(target, observer));

        /// <summary>
        /// Stream of intersection shapes between the moving object and a stationary shape.
        /// </summary>
        public IObservable<PlanarShape> IntersectionWith(PlanarShape target)
            => Observable.Create<PlanarShape>(observer => source.RxIntersection(target, observer));

        /// <summary>
        /// Stream of intersection shapes between two moving objects.
        /// </summary>
        public IObservable<PlanarShape> IntersectionWith(RxGeometry target)
            => Observable.Create<PlanarShape>(observer => source.RxRxIntersection(target, observer));
    }
}
