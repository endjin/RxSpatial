using System.Data.SqlTypes;
using System.Reactive.Linq;
using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>
/// Bridges the RxSpatial subscription API to composable System.Reactive (Rx.NET v7)
/// observable sequences, so spatio-temporal streams can be combined with the full Rx
/// operator surface (Where, Sample, Throttle, DistinctUntilChanged, ...). Implemented
/// with C# 14 extension members.
/// </summary>
public static class RxGeographyObservable
{
    extension(RxGeography source)
    {
        /// <summary>
        /// Stream of raw location updates published by the moving object. Location updates are
        /// forwarded to direct subscribers when the <c>isVisualized</c> application setting is
        /// <c>true</c> (the original RxSpatial behaviour).
        /// </summary>
        public IObservable<SqlGeography> Locations
            => Observable.Create<SqlGeography>(observer => source.Subscribe(observer));

        /// <summary>
        /// Stream of distances (in metres) from the moving object to a stationary geography,
        /// re-evaluated on every location update of the moving object.
        /// </summary>
        public IObservable<SqlDouble> DistanceTo(SqlGeography target)
            => Observable.Create<SqlDouble>(observer => source.RxDistance(target, observer));

        /// <summary>
        /// Stream of distances (in metres) between two moving objects, re-evaluated on every
        /// location update of the moving object.
        /// </summary>
        public IObservable<SqlDouble> DistanceTo(RxGeography target)
            => Observable.Create<SqlDouble>(observer => source.RxRxDistance(target, observer));

        /// <summary>
        /// Stream of intersection tests between the moving object and a stationary geography.
        /// </summary>
        public IObservable<bool> Intersects(SqlGeography target)
            => Observable.Create<bool>(observer => source.RxIntersect(target, observer));

        /// <summary>
        /// Stream of intersection tests between two moving objects.
        /// </summary>
        public IObservable<bool> Intersects(RxGeography target)
            => Observable.Create<bool>(observer => source.RxRxIntersect(target, observer));

        /// <summary>
        /// Stream of intersection geometries between the moving object and a stationary geography.
        /// </summary>
        public IObservable<SqlGeography> IntersectionWith(SqlGeography target)
            => Observable.Create<SqlGeography>(observer => source.RxIntersection(target, observer));

        /// <summary>
        /// Stream of intersection geometries between two moving objects.
        /// </summary>
        public IObservable<SqlGeography> IntersectionWith(RxGeography target)
            => Observable.Create<SqlGeography>(observer => source.RxRxIntersection(target, observer));
    }
}
