// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using RxSpatial.Backends;
using RxSpatial.Geo;
using RxSpatial.Index;

namespace RxSpatial;

/// <summary>
/// Geodetic operations for <see cref="RxGeography"/>: the WGS84 shape maths of
/// <see cref="GeoOperations"/>, with rectangles in longitude (X) and latitude (Y).
/// </summary>
internal readonly struct GeographyOperations : ISpatialOperations<GeoShape>
{
    /// <summary>
    /// The shape's degree bounds. Throws <see cref="InvalidOperationException"/> for
    /// <see cref="GeoEmpty"/>, which has none — as an empty SQL Server geography did — so an
    /// empty location cannot be pushed on the grid and tree backends.
    /// </summary>
    public static Rect BoundsOf(GeoShape shape)
    {
        GeoBounds bounds = GeoOperations.Bounds(shape);
        return new Rect(bounds.MinLongitude, bounds.MinLatitude, bounds.MaxLongitude, bounds.MaxLatitude);
    }

    public static bool Intersects(GeoShape a, GeoShape b) => GeoOperations.Intersects(a, b);

    public static GeoShape Intersection(GeoShape a, GeoShape b) => GeoOperations.Intersection(a, b);

    /// <summary>
    /// The geodesic distance in metres, or <see cref="double.NaN"/> when there is none: an
    /// empty shape, or a nearly antipodal pair whose geodesic does not converge.
    /// </summary>
    public static double Distance(GeoShape a, GeoShape b) => GeoOperations.Distance(a, b);

    public static GridLayout GridLayoutOf(RxSpatialOptions options) => GridLayout.From(options.Grid);
}
