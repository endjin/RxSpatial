// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using static System.FormattableString;

namespace RxSpatial.Geo;

/// <summary>A position on the WGS84 ellipsoid, in degrees.</summary>
public readonly record struct GeoPoint
{
    /// <summary>Creates a position.</summary>
    /// <param name="latitude">Degrees north, from -90 to 90.</param>
    /// <param name="longitude">Degrees east, from -180 to 180.</param>
    /// <exception cref="ArgumentOutOfRangeException">A coordinate is NaN or out of range.</exception>
    public GeoPoint(double latitude, double longitude)
    {
        if (!(latitude is >= -90 and <= 90))
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90 degrees.");
        if (!(longitude is >= -180 and <= 180))
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180 degrees.");

        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>Degrees north of the equator (negative south).</summary>
    public double Latitude { get; }

    /// <summary>Degrees east of the prime meridian (negative west).</summary>
    public double Longitude { get; }

    /// <summary>The position as <c>(latitude, longitude)</c> in the invariant culture.</summary>
    public override string ToString() => Invariant($"({Latitude}, {Longitude})");
}
