// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial.Geo;

/// <summary>
/// The planar shape with no points: what <see cref="PlanarOperations.Intersection"/> returns
/// for disjoint shapes. Distances to it are <see cref="double.NaN"/>.
/// </summary>
public sealed class PlanarEmpty : PlanarShape
{
    private PlanarEmpty()
        : base(ShapeKind.Empty)
    {
    }

    /// <summary>The single instance.</summary>
    public static PlanarEmpty Instance { get; } = new();

    /// <inheritdoc/>
    public override string ToString() => "POLYGON EMPTY";
}
