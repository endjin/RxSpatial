using System;
using System.Data.SqlTypes;
using System.Reactive.Linq;
using Microsoft.SqlServer.Types;

namespace RxSpatial
{
    /// <summary>
    /// Bridges the RxSpatial subscription API to composable System.Reactive (Rx.NET v7)
    /// observable sequences, so spatio-temporal streams can be combined with the full
    /// Rx operator surface (Where, Sample, Throttle, DistinctUntilChanged, ...).
    /// </summary>
    public static class RxGeographyObservable
    {
        /// <summary>
        /// Stream of distances (in metres) from the moving object to a stationary geography,
        /// re-evaluated on every location update of <paramref name="source"/>.
        /// </summary>
        public static IObservable<SqlDouble> DistanceTo(this RxGeography source, SqlGeography target)
            => Observable.Create<SqlDouble>(observer => source.RxDistance(target, observer));

        /// <summary>
        /// Stream of distances (in metres) between two moving objects, re-evaluated on
        /// every location update of <paramref name="source"/>.
        /// </summary>
        public static IObservable<SqlDouble> DistanceTo(this RxGeography source, RxGeography target)
            => Observable.Create<SqlDouble>(observer => source.RxRxDistance(target, observer));

        /// <summary>
        /// Stream of intersection tests between the moving object and a stationary geography.
        /// </summary>
        public static IObservable<bool> Intersects(this RxGeography source, SqlGeography target)
            => Observable.Create<bool>(observer => source.RxIntersect(target, observer));

        /// <summary>
        /// Stream of intersection tests between two moving objects.
        /// </summary>
        public static IObservable<bool> Intersects(this RxGeography source, RxGeography target)
            => Observable.Create<bool>(observer => source.RxRxIntersect(target, observer));

        /// <summary>
        /// Stream of intersection geometries between the moving object and a stationary geography.
        /// </summary>
        public static IObservable<SqlGeography> IntersectionWith(this RxGeography source, SqlGeography target)
            => Observable.Create<SqlGeography>(observer => source.RxIntersection(target, observer));

        /// <summary>
        /// Stream of intersection geometries between two moving objects.
        /// </summary>
        public static IObservable<SqlGeography> IntersectionWith(this RxGeography source, RxGeography target)
            => Observable.Create<SqlGeography>(observer => source.RxRxIntersection(target, observer));

        /// <summary>
        /// Stream of raw location updates published by the moving object. Location updates are
        /// forwarded to direct subscribers when the <c>isVisualized</c> application setting is
        /// <c>true</c> (the original RxSpatial behaviour).
        /// </summary>
        public static IObservable<SqlGeography> Locations(this RxGeography source)
            => Observable.Create<SqlGeography>(observer => source.Subscribe(observer));
    }
}
