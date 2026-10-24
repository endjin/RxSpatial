// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial;

/// <summary>Dimensions of the grid (spatial hash) backend for planar <see cref="RxGeometry"/> objects.</summary>
public sealed class PlanarGridOptions
{
    /// <summary>Number of grid cells along the X axis.</summary>
    public int XCells { get; set; } = 10;

    /// <summary>Number of grid cells along the Y axis.</summary>
    public int YCells { get; set; } = 10;

    /// <summary>The planar rectangle the grid covers, in the geometries' coordinate units.</summary>
    public PlanarCoverageRegionOptions CoverageRegion { get; set; } = new();
}
