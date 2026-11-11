# RxSpatial

Reactive geospatial processing for .NET. Observe things in motion, and react when something interesting happens.

Each moving object is an Rx.NET observer of its own position and an observable source of query results. You describe a spatial question once. Every time the object reports a new position, the question is re-evaluated and the answer is pushed to you. Your code never polls.

```csharp
using System.Reactive.Linq;
using RxSpatial;
using RxSpatial.Geo;

// A 500 m fence around the Seattle Space Needle.
var fence = new GeoCircle(new GeoPoint(47.6205, -122.3493), radiusMetres: 500);

// IsVisualized = true handles each position as it arrives, on the calling thread.
var vehicle = new RxGeography(new RxSpatialOptions { IsVisualized = true });

using var alerts = vehicle.DistanceTo(fence)
                          .Where(metres => metres < 1000)
                          .Subscribe(m => Console.WriteLine($"{m:F0} m from the fence"));

vehicle.OnNext(new GeoPointShape(47.6400, -122.3493));   // too far away: nothing is printed
vehicle.OnNext(new GeoPointShape(47.6260, -122.3493));   // prints "112 m from the fence"
vehicle.OnNext(new GeoPointShape(47.6210, -122.3493));   // prints "0 m from the fence": it is inside
```

## What you can ask

There are three questions. Each can be asked about a moving object and a fixed area, or about two moving objects.

| Question | Moving object and a fixed area | Two moving objects | Each answer is |
| --- | --- | --- | --- |
| Do they intersect? | `a.Intersects(area)` | `a.Intersects(b)` | `bool` |
| How far apart are they? | `a.DistanceTo(area)` | `a.DistanceTo(b)` | `double` |
| What area do they share? | `a.IntersectionWith(area)` | `a.IntersectionWith(b)` | a shape |

A moving object is one of two classes:

- `RxGeography` works in latitude and longitude on the WGS84 ellipsoid. Distances are in metres.
- `RxGeometry` works in flat X/Y coordinates, for a warehouse floor, a CAD drawing or a projected map. Distances are in your own units.

## Things to know

- Polygons must be convex. Geodetic shapes are at most 500 km across.
- A distance is `double.NaN` when it cannot be computed: for an empty shape, and for a nearly antipodal pair of points.
- By default a moving object buffers positions and processes them on another thread, and `OnNext` throws `RxSpatialOverloadException` when more than `RxSpatialOptions.QueueLimit` positions are waiting (the default limit is 1). Set `IsVisualized = true`, as above, to process each position synchronously.
- `RxSpatialOptions.PreferredDataStruct` chooses the spatial index: a list (the default), a grid, an R*-tree or a RUM-tree. The answers are the same on each; the cost of an update is not.

## More

- [Using RxSpatial](https://github.com/endjin/RxSpatial/blob/main/docs/usage.md): the query API in full, `RxGeometry` and the shape types.
- [Configuration](https://github.com/endjin/RxSpatial/blob/main/docs/configuration.md): `RxSpatialOptions` and binding it from `appsettings.json`.
- [The repository](https://github.com/endjin/RxSpatial): the source, a map demo, benchmarks and the research papers.

This package depends on [RxSpatial.Geo](https://github.com/endjin/RxSpatial/tree/main/solutions/RxSpatial.Geo) (the shapes) and [RxSpatial.Index](https://github.com/endjin/RxSpatial/tree/main/solutions/RxSpatial.Index) (the spatial indexes).

RxSpatial began as a research project by Abdeltawab M. Hendawi, Youying Shi, Hossam Fattah, Jayant Gupta, Jumana Karwa and Mohamed H. Ali (ACM SIGMOD, DEBS and SIGSPATIAL 2016, IEEE ICDE 2017), and was modernised by endjin in 2026. It is released under the MIT licence.
