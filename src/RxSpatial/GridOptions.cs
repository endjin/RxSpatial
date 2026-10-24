// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial;

/// <summary>Dimensions of the grid (spatial hash) backend.</summary>
public sealed class GridOptions
{
    /// <summary>Number of grid cells along the latitude axis.</summary>
    public int LatitudeCells { get; set; } = 10;

    /// <summary>Number of grid cells along the longitude axis.</summary>
    public int LongitudeCells { get; set; } = 10;

    /// <summary>The geographic rectangle the grid covers.</summary>
    public CoverageRegionOptions CoverageRegion { get; set; } = new();
}
