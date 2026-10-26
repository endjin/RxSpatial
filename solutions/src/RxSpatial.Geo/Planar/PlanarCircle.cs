// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using static System.FormattableString;

namespace RxSpatial.Geo;

/// <summary>
/// A disc: every point within <see cref="Radius"/> of <see cref="Center"/>. It is an exact
/// shape, not a polygon; <see cref="PlanarOperations.ToVertices(PlanarShape, int)"/> polygonises it for drawing.
/// </summary>
public sealed class PlanarCircle : PlanarShape
{
    /// <summary>Creates a disc.</summary>
    /// <param name="center">The centre; both coordinates must be finite.</param>
    /// <param name="radius">The radius; finite and greater than zero.</param>
    /// <exception cref="ArgumentException">A coordinate is NaN or infinite.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The radius is not finite and positive.</exception>
    public PlanarCircle(PlanarPoint center, double radius)
        : base(ShapeKind.Circle)
    {
        if (!center.IsFinite)
            throw new ArgumentException("Coordinates must be finite.", nameof(center));
        if (!double.IsFinite(radius) || radius <= 0)
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "The radius must be finite and greater than zero.");

        Center = center;
        Radius = radius;
    }

    /// <summary>The centre.</summary>
    public PlanarPoint Center { get; }

    /// <summary>The radius, in the plane's units.</summary>
    public double Radius { get; }

    /// <inheritdoc/>
    public override string ToString() => Invariant($"CIRCLE ({Center.X} {Center.Y}, {Radius})");
}
