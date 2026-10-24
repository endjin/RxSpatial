// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial;

/// <summary>A geographic rectangle, expressed by its lower-left and upper-right corners.</summary>
public sealed class CoverageRegionOptions
{
    public double LowerLeftLatitude { get; set; } = 46;

    public double LowerLeftLongitude { get; set; } = -124;

    public double UpperRightLatitude { get; set; } = 49;

    public double UpperRightLongitude { get; set; } = -120;
}
