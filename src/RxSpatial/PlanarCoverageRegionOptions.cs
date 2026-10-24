// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial;

/// <summary>A planar rectangle, expressed by its minimum and maximum X and Y coordinates.</summary>
public sealed class PlanarCoverageRegionOptions
{
    public double MinX { get; set; }

    public double MinY { get; set; }

    public double MaxX { get; set; } = 1000;

    public double MaxY { get; set; } = 1000;
}
