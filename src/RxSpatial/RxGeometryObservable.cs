using System.Reactive.Linq;
using Microsoft.SqlServer.Types;

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
        public IObservable<SqlGeometry> Locations
            => Observable.Create<SqlGeometry>(observer => source.Subscribe(observer));

        /// <summary>
        /// Stream of Euclidean distances (in the geometries' coordinate units) from the moving
        /// object to a stationary geometry, re-evaluated on every location update of the moving
        /// object. A value is <see cref="double.NaN"/> when SQL Server cannot compute a
        /// distance, e.g. different SRIDs or an empty geometry.
        /// </summary>
        public IObservable<double> DistanceTo(SqlGeometry target)
            => Observable.Create<double>(observer => source.RxDistance(target, observer));

        /// <summary>
        /// Stream of Euclidean distances (in the geometries' coordinate units) between two
        /// moving objects, re-evaluated on every location update of the moving object. A value
        /// is <see cref="double.NaN"/> when SQL Server cannot compute a distance, e.g.
        /// different SRIDs or an empty geometry.
        /// </summary>
        public IObservable<double> DistanceTo(RxGeometry target)
            => Observable.Create<double>(observer => source.RxRxDistance(target, observer));

        /// <summary>
        /// Stream of intersection tests between the moving object and a stationary geometry.
        /// </summary>
        public IObservable<bool> Intersects(SqlGeometry target)
            => Observable.Create<bool>(observer => source.RxIntersect(target, observer));

        /// <summary>
        /// Stream of intersection tests between two moving objects.
        /// </summary>
        public IObservable<bool> Intersects(RxGeometry target)
            => Observable.Create<bool>(observer => source.RxRxIntersect(target, observer));

        /// <summary>
        /// Stream of intersection geometries between the moving object and a stationary geometry.
        /// </summary>
        public IObservable<SqlGeometry> IntersectionWith(SqlGeometry target)
            => Observable.Create<SqlGeometry>(observer => source.RxIntersection(target, observer));

        /// <summary>
        /// Stream of intersection geometries between two moving objects.
        /// </summary>
        public IObservable<SqlGeometry> IntersectionWith(RxGeometry target)
            => Observable.Create<SqlGeometry>(observer => source.RxRxIntersection(target, observer));
    }
}
