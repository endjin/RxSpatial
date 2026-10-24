// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace RxSpatial.Geo;

/// <summary>
/// The bounding rectangle of a <see cref="GeoShape"/> in degrees: longitude as X, latitude
/// as Y, the axes a spatial index uses. Bounds are conservative: the shape lies inside them.
/// </summary>
/// <remarks>
/// Bounds are not split at the antimeridian. A shape that crosses ±180° gets a longitude
/// range that runs past 180 (or below −180) instead of two rectangles, and a shape that
/// contains a pole spans all longitudes.
/// </remarks>
/// <param name="MinLongitude">The westernmost longitude.</param>
/// <param name="MinLatitude">The southernmost latitude.</param>
/// <param name="MaxLongitude">The easternmost longitude.</param>
/// <param name="MaxLatitude">The northernmost latitude.</param>
public readonly record struct GeoBounds(double MinLongitude, double MinLatitude, double MaxLongitude, double MaxLatitude)
{
    private double CenterLongitude => (MinLongitude + MaxLongitude) * 0.5;

    private double HalfWidth => (MaxLongitude - MinLongitude) * 0.5;

    /// <summary>Whether the point lies inside, comparing longitudes across the antimeridian.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Contains(GeoPoint point)
        => point.Latitude >= MinLatitude
        && point.Latitude <= MaxLatitude
        && (HalfWidth >= 180 || Math.Abs(Wgs84.LongitudeDifference(CenterLongitude, point.Longitude)) <= HalfWidth);

    /// <summary>Closed-interval overlap, comparing longitudes across the antimeridian.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Overlaps(in GeoBounds other)
        => other.MinLatitude <= MaxLatitude
        && other.MaxLatitude >= MinLatitude
        && (HalfWidth + other.HalfWidth >= 180
            || Math.Abs(Wgs84.LongitudeDifference(CenterLongitude, other.CenterLongitude)) <= HalfWidth + other.HalfWidth);
}
