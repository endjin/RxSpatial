// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial.Geo;

/// <summary>
/// A shape on the WGS84 ellipsoid: a <see cref="GeoPointShape"/>, a geodesic
/// <see cref="GeoCircle"/>, a convex <see cref="GeoPolygon"/> with geodesic edges, or
/// <see cref="GeoEmpty"/>. The hierarchy is closed; <see cref="GeoOperations"/> holds the
/// maths. Shapes are immutable and safe to share between threads.
/// </summary>
/// <remarks>
/// Circles and polygons may be at most <see cref="MaxExtent"/> across: the operations work
/// in a local plane whose distortion grows with size.
/// </remarks>
public abstract class GeoShape
{
    /// <summary>The largest distance, in metres, allowed across a circle or polygon (500 km).</summary>
    public const double MaxExtent = 500_000;

    private protected GeoShape(ShapeKind kind) => Kind = kind;

    /// <summary>Whether this is <see cref="GeoEmpty"/>, the shape with no points.</summary>
    public bool IsEmpty => Kind == ShapeKind.Empty;

    internal ShapeKind Kind { get; }
}
