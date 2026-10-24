// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using System.Reactive.Linq;
using RxSpatial.Geo;

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
        /// forwarded to direct subscribers when <see cref="RxSpatialOptions.IsVisualized"/> is
        /// <c>true</c> (the original RxSpatial behaviour).
        /// </summary>
        public IObservable<GeoShape> Locations
            => Observable.Create<GeoShape>(observer => source.Subscribe(observer));

        /// <summary>
        /// Stream of geodesic distances (in metres) from the moving object to a stationary
        /// shape, re-evaluated on every location update of the moving object. A value is
        /// <see cref="double.NaN"/> when there is no distance: an empty shape, or a nearly
        /// antipodal pair whose geodesic does not converge.
        /// </summary>
        public IObservable<double> DistanceTo(GeoShape target)
            => Observable.Create<double>(observer => source.RxDistance(target, observer));

        /// <summary>
        /// Stream of geodesic distances (in metres) between two moving objects, re-evaluated on
        /// every location update of the moving object. A value is <see cref="double.NaN"/> when
        /// there is no distance: an empty shape, or a nearly antipodal pair whose geodesic does
        /// not converge.
        /// </summary>
        public IObservable<double> DistanceTo(RxGeography target)
            => Observable.Create<double>(observer => source.RxRxDistance(target, observer));

        /// <summary>
        /// Stream of intersection tests between the moving object and a stationary shape.
        /// </summary>
        public IObservable<bool> Intersects(GeoShape target)
            => Observable.Create<bool>(observer => source.RxIntersect(target, observer));

        /// <summary>
        /// Stream of intersection tests between two moving objects.
        /// </summary>
        public IObservable<bool> Intersects(RxGeography target)
            => Observable.Create<bool>(observer => source.RxRxIntersect(target, observer));

        /// <summary>
        /// Stream of intersection shapes between the moving object and a stationary shape.
        /// </summary>
        public IObservable<GeoShape> IntersectionWith(GeoShape target)
            => Observable.Create<GeoShape>(observer => source.RxIntersection(target, observer));

        /// <summary>
        /// Stream of intersection shapes between two moving objects.
        /// </summary>
        public IObservable<GeoShape> IntersectionWith(RxGeography target)
            => Observable.Create<GeoShape>(observer => source.RxRxIntersection(target, observer));
    }
}
