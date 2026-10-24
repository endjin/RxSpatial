// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial.Geo;

/// <summary>The closed set of shape kinds, for pair dispatch without type tests.</summary>
internal enum ShapeKind : byte
{
    Empty,
    Point,
    Circle,
    Polygon,
}
