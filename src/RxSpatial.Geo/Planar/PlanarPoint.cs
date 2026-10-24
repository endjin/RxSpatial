// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace RxSpatial.Geo;

/// <summary>
/// A point in a flat plane. For <see cref="PlanarShape"/> the units are the caller's own;
/// for a <see cref="LocalPlane"/> they are metres east (<see cref="X"/>) and north
/// (<see cref="Y"/>) of the plane's origin.
/// </summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
public readonly record struct PlanarPoint(double X, double Y)
{
    /// <summary>The Euclidean distance to <paramref name="other"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double DistanceTo(PlanarPoint other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    internal double Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Math.Sqrt((X * X) + (Y * Y));
    }

    internal bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
}
