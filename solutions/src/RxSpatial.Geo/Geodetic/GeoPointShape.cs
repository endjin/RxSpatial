// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using static System.FormattableString;

namespace RxSpatial.Geo;

/// <summary>A single position: a vehicle's location, for example.</summary>
/// <param name="position">The position.</param>
public sealed class GeoPointShape(GeoPoint position) : GeoShape(ShapeKind.Point)
{
    /// <summary>Creates a point shape at a latitude and longitude.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is NaN or out of range.</exception>
    public GeoPointShape(double latitude, double longitude)
        : this(new GeoPoint(latitude, longitude))
    {
    }

    /// <summary>The position.</summary>
    public GeoPoint Position { get; } = position;

    /// <summary>The point as WKT (longitude first) in the invariant culture.</summary>
    public override string ToString() => Invariant($"POINT ({Position.Longitude} {Position.Latitude})");
}
