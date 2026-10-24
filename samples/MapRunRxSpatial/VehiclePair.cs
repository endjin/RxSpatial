// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace MapRunRxSpatial;

/// <summary>Order-independent identity of a watched vehicle pair.</summary>
internal readonly record struct VehiclePair(int First, int Second)
{
    public static VehiclePair Of(int a, int b) => a < b ? new(a, b) : new(b, a);

    /// <summary>Id of the pair's connection line on the map.</summary>
    public string ShapeId => FormattableString.Invariant($"pair-{First}-{Second}");

    /// <summary>Key of the pair's subscription on each vehicle (distinct from area keys).</summary>
    public string SubscriptionKey => FormattableString.Invariant($"pair:{First}-{Second}");
}
