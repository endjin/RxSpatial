// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial.Geo;

/// <summary>The axis-aligned bounding rectangle of a <see cref="PlanarShape"/>.</summary>
/// <param name="MinX">The smallest X coordinate.</param>
/// <param name="MinY">The smallest Y coordinate.</param>
/// <param name="MaxX">The largest X coordinate.</param>
/// <param name="MaxY">The largest Y coordinate.</param>
public readonly record struct PlanarBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    /// <summary>Closed-interval overlap: rectangles that merely touch do overlap.</summary>
    internal bool Overlaps(in PlanarBounds other)
        => other.MinX <= MaxX && other.MaxX >= MinX && other.MinY <= MaxY && other.MaxY >= MinY;
}
