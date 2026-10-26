// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using RxSpatial.Index;

namespace RxSpatial.Backends;

/// <summary>
/// The shape-agnostic geometry of a grid backend: the rectangle it covers and its cell counts.
/// Rows run along Y (latitude for geographies), columns along X (longitude).
/// </summary>
internal readonly record struct GridLayout(Rect Extent, int XCells, int YCells)
{
    /// <summary>The layout of the geographic grid: X is longitude, Y is latitude.</summary>
    public static GridLayout From(GridOptions options)
        => new(
            new Rect(
                options.CoverageRegion.LowerLeftLongitude,
                options.CoverageRegion.LowerLeftLatitude,
                options.CoverageRegion.UpperRightLongitude,
                options.CoverageRegion.UpperRightLatitude),
            options.LongitudeCells,
            options.LatitudeCells);

    /// <summary>The layout of the planar grid, in the geometries' coordinate units.</summary>
    public static GridLayout From(PlanarGridOptions options)
        => new(
            new Rect(
                options.CoverageRegion.MinX,
                options.CoverageRegion.MinY,
                options.CoverageRegion.MaxX,
                options.CoverageRegion.MaxY),
            options.XCells,
            options.YCells);
}
