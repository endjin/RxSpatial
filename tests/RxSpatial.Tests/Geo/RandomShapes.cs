// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using RxSpatial.Geo;

namespace RxSpatial.Tests.Geo;

/// <summary>Seeded random shapes for the property tests.</summary>
internal sealed class RandomShapes(int seed)
{
    private readonly Random random = new(seed);

    public double Between(double min, double max) => min + (random.NextDouble() * (max - min));

    public int Next(int min, int max) => random.Next(min, max);

    /// <summary>A centre in Seattle, or (one time in three) anywhere up to 70° latitude, including the antimeridian.</summary>
    public GeoPoint Centre()
    {
        return random.Next(3) switch
        {
            0 => new(Between(-70, 70), Between(-180, 180)),
            1 => new(Between(-1, 1), random.Next(2) == 0 ? Between(179.9, 180) : Between(-180, -179.9)),
            _ => new(47.6 + Between(-0.05, 0.05), -122.33 + Between(-0.05, 0.05)),
        };
    }

    /// <summary>A position up to <paramref name="spread"/> metres from <paramref name="centre"/>.</summary>
    public GeoPoint Near(GeoPoint centre, double spread)
        => Wgs84.Direct(centre, Between(0, 360), spread * Math.Sqrt(random.NextDouble()));

    /// <summary>A random point, circle or polygon of up to <paramref name="size"/> metres across, near <paramref name="centre"/>.</summary>
    public GeoShape Shape(GeoPoint centre, double size)
    {
        GeoPoint at = Near(centre, size);
        return random.Next(3) switch
        {
            0 => new GeoPointShape(at),
            1 => new GeoCircle(at, Between(size / 50, size / 2)),
            _ => Polygon(at, Between(size / 25, size / 2)),
        };
    }

    /// <summary>
    /// A convex polygon: 3–9 vertices on a rotated ellipse of semi-axes up to
    /// <paramref name="radius"/> metres, in the local plane of <paramref name="centre"/>.
    /// Points on an ellipse are always in convex position.
    /// </summary>
    public GeoPolygon Polygon(GeoPoint centre, double radius)
    {
        int count = random.Next(3, 10);
        double rx = radius;
        double ry = radius * Between(0.3, 1);
        double rotation = Between(0, Math.PI);
        LocalPlane plane = new(centre);
        GeoPoint[] vertices = new GeoPoint[count];
        for (int i = 0; i < count; i++)
        {
            double angle = (2 * Math.PI * (i + Between(0.1, 0.9))) / count;
            double x = rx * Math.Cos(angle);
            double y = ry * Math.Sin(angle);
            (double sin, double cos) = Math.SinCos(rotation);
            vertices[i] = plane.Unproject(new((x * cos) - (y * sin), (x * sin) + (y * cos)));
        }

        return GeoPolygon.FromVertices(vertices);
    }

    /// <summary>A random planar point, circle or polygon of up to <paramref name="size"/> across, near (cx, cy).</summary>
    public PlanarShape Planar(double cx, double cy, double size)
    {
        PlanarPoint at = new(cx + Between(-size, size), cy + Between(-size, size));
        return random.Next(3) switch
        {
            0 => new PlanarPointShape(at),
            1 => new PlanarCircle(at, Between(size / 50, size / 2)),
            _ => PlanarConvex(at, Between(size / 25, size / 2)),
        };
    }

    public PlanarPolygon PlanarConvex(PlanarPoint centre, double radius)
    {
        int count = random.Next(3, 10);
        double ry = radius * Between(0.3, 1);
        double rotation = Between(0, Math.PI);
        (double sin, double cos) = Math.SinCos(rotation);
        PlanarPoint[] vertices = new PlanarPoint[count];
        for (int i = 0; i < count; i++)
        {
            double angle = (2 * Math.PI * (i + Between(0.1, 0.9))) / count;
            double x = radius * Math.Cos(angle);
            double y = ry * Math.Sin(angle);
            vertices[i] = new(centre.X + (x * cos) - (y * sin), centre.Y + (x * sin) + (y * cos));
        }

        return PlanarPolygon.FromVertices(vertices);
    }
}
