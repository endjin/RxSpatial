// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial.Geo;

/// <summary>
/// A shape in a flat plane, in the caller's own units: a <see cref="PlanarPointShape"/>, a
/// <see cref="PlanarCircle"/>, a convex <see cref="PlanarPolygon"/> or
/// <see cref="PlanarEmpty"/>. The hierarchy is closed; <see cref="PlanarOperations"/> holds
/// the maths. Shapes are immutable and safe to share between threads.
/// </summary>
public abstract class PlanarShape
{
    private protected PlanarShape(ShapeKind kind) => Kind = kind;

    /// <summary>Whether this is <see cref="PlanarEmpty"/>, the shape with no points.</summary>
    public bool IsEmpty => Kind == ShapeKind.Empty;

    internal ShapeKind Kind { get; }
}
