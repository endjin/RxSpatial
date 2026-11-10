# Using RxSpatial

## Using RxSpatial with System.Reactive v7

`RxGeography` implements `IObserver<GeoShape>` / `IObservable<GeoShape>`, so it composes directly with Rx.NET. The library also ships `RxGeographyObservable` — C# 14 extension members that expose the continuous query API as ordinary observable sequences:

```csharp
using RxSpatial;
using RxSpatial.Geo;

var fence = new GeoCircle(new GeoPoint(47.6205, -122.3493), radiusMetres: 500);
var vehicle = new RxGeography();

// Consume continuous spatial queries with Rx operators...
using var alerts = vehicle.DistanceTo(fence)
                          .DistinctUntilChanged()
                          .Where(metres => metres < 1000)
                          .Subscribe(m => Console.WriteLine($"{m:F0} m from the fence"));

// ...observe the raw position stream...
using var track = vehicle.Locations.Subscribe(p => Plot(p));

// ...and feed positions from any Rx sequence: RxGeography is an IObserver.
gpsFixes.Select(fix => new GeoPointShape(fix.Lat, fix.Lon))
        .Subscribe(vehicle);
```

Continuous queries between two moving objects (`RxRx*` in the papers) are available through the same extensions: `a.DistanceTo(b)`, `a.Intersects(b)`, `a.IntersectionWith(b)`.

Distance queries (`RxDistance`, `RxRxDistance` and both `DistanceTo` overloads) report metres as `double`, as the papers declare them (`IObserver<double>`). When a distance cannot be computed, the value is `double.NaN`, so every location update still produces exactly one notification. In the core this happens for an empty shape (`GeoEmpty`) and for a nearly antipodal pair of points, where the geodesic solver does not converge; in the `RxSql*` facades it happens when SQL Server's `STDistance` returns Null (different SRIDs, an empty geography). Filter with `double.IsNaN` if your pipeline must not see it.

The papers name the two families by their operands: a single `Rx` prefix (`RxIntersect`, `RxDistance`, `RxIntersection`) relates a moving object to a stationary geography, and a double `RxRx` prefix relates two moving objects. The method names follow the SIGMOD 2016 class diagram; the ICDE 2017 paper spells them `RxIntersects` / `RxRxIntersects`, which are the same operations.

## Planar moving objects: `RxGeometry`

The ICDE 2017 paper (§III) names a second moving-object class beside `RxGeography`: "the same concepts would apply to the RxGeometry class". The authors never released it, so `RxGeometry` here is a clean-room version of that contract. It is the planar sibling of `RxGeography` (over `PlanarShape`), with the same six queries (`RxIntersect`, `RxIntersection`, `RxDistance` and their `RxRx*` forms), the same `Location`, `AverageLatency` and `QueuedPushCount`, and `RxGeometryObservable` extension members that mirror `RxGeographyObservable` (`Locations`, `DistanceTo`, `Intersects`, `IntersectionWith`). Use it for flat coordinate systems: a warehouse or factory floor, a CAD drawing, or a projected map.

```csharp
// A forklift on a warehouse floor (metres) approaching a 100 m loading bay.
var bay = PlanarPolygon.Rectangle(minX: 450, minY: 450, maxX: 550, maxY: 550);
var forklift = new RxGeometry();

using var nearBay = forklift.DistanceTo(bay)
                            .Where(metres => metres < 100)
                            .Subscribe(m => Console.WriteLine($"{m:F0} m from the bay"));

forklift.OnNext(new PlanarPointShape(100, 500));   // 350 m
forklift.OnNext(new PlanarPointShape(400, 500));   // 50 m: reported
```

- **Units.** Distances are Euclidean, in the shapes' own coordinate units (metres in the example), not geodetic metres. The planar maths is exact.
- **NaN rule.** A distance is `double.NaN` when either shape is `PlanarEmpty`.
- **Shared engine.** Both classes implement `IMovingObject<TShape>` (an `IObservable<TShape>` with a `Location`) and share one internal engine, so the push queue, the drainer and every index backend behave the same. The grid backend of an `RxGeometry` covers `RxSpatialOptions.PlanarGrid` instead of the latitude/longitude `Grid` (see [Configuration](configuration.md)).
- **No mixing.** An `RxGeometry` cannot query an `RxGeography`, or the reverse: the two classes have no common base type, so mixing them is a compile error by design.
- **Naming.** `RxGeographyDataStruct` keeps its historic name and selects the index for both classes.

## Shapes: `RxSpatial.Geo`

`RxSpatial.Geo` is the shape library under the core: a clean-room, zero-dependency replacement for the parts of SQL Server's spatial types that RxSpatial uses. It runs wherever .NET 10 runs. Every type is in the `RxSpatial.Geo` namespace.

| Geodetic (WGS84, metres) | Planar (Euclidean, your units) | Notes |
| --- | --- | --- |
| `GeoPoint(latitude, longitude)` | `PlanarPoint(x, y)` | A coordinate. `GeoPoint` validates its range and takes latitude first. |
| `GeoPointShape` | `PlanarPointShape` | A position, for example a GPS fix. |
| `GeoCircle(center, radiusMetres)` | `PlanarCircle(center, radius)` | An **exact** circle, not a polygon: a geofence, a reach, a buffered position. |
| `GeoPolygon.Rectangle(south, west, north, east)`, `.FromVertices(...)`, `.FromWkt("POLYGON ((lon lat, ...))")` | `PlanarPolygon.Rectangle(minX, minY, maxX, maxY)`, `.FromVertices(...)`, `.FromWkt(...)` | A **convex** polygon. Either winding order is accepted and stored counter-clockwise; `ToString()` gives WKT. |
| `GeoEmpty.Instance` | `PlanarEmpty.Instance` | The empty shape (an intersection that does not overlap). |

`GeoOperations` and `PlanarOperations` hold the maths: `Intersects`, `Intersection`, `Distance`, `Area`, `Bounds` and `ToVertices` (an outline for drawing or export, 128 segments per circle by default, like SQL Server's buffer; an overload writes into a `Span` without allocating, for redrawing on every update). `Wgs84` exposes the geodesic itself (`Inverse`, `Distance`, `Direct`).

```csharp
using RxSpatial.Geo;

var needle = new GeoPoint(47.6205, -122.3493);
var fence = new GeoCircle(needle, radiusMetres: 500);
var downtown = GeoPolygon.Rectangle(south: 47.600, west: -122.370, north: 47.618, east: -122.330);

bool inside = GeoOperations.Intersects(fence, new GeoPointShape(47.6230, -122.3480));   // true
double metres = GeoOperations.Distance(fence, downtown);                                 // 0 if they touch
GeoShape overlap = GeoOperations.Intersection(fence, downtown);                          // a convex GeoPolygon, the fence, or GeoEmpty
double squareMetres = GeoOperations.Area(overlap);                                       // the exact curved area
GeoPoint[] outline = GeoOperations.ToVertices(fence);                                    // 128 points on the circle
```

How it computes, and its limits:

- **Geodesics** are Vincenty's inverse and direct solutions on the WGS84 ellipsoid, converged to 1e-14 within 200 iterations. A nearly antipodal pair that does not converge gives `double.NaN`, never a wrong distance. A fast flat-earth estimate (the ellipsoid's radii of curvature at the mean latitude) with a 0.5 % guard band answers the clear cases of a circle test without the full solution.
- **Circles are exact.** Point-in-circle and the distances to a circle use the geodesic. The intersection of two circles is the analytic lens, returned as a `GeoPolygon` whose arcs are vertices at most 2π/128 apart; its `Area` is the exact area of the curved lens, not of the vertices.
- **Polygons are convex only.** A concave or self-intersecting ring throws `ArgumentException`. Point-in-polygon and the distances to a polygon are exact at any size. Polygon–polygon overlap, and the clipping inside `Intersection`, run in a local azimuthal-equidistant plane (`LocalPlane`).
- **Shapes are at most 500 km across** (`GeoShape.MaxExtent`; a circle's radius is at most 250 km), because the local plane is accurate only at that scale.
- **The antimeridian is not split.** `Bounds` is in degrees and does not wrap at ±180°, as before with SQL Server.
- **Hot paths do not allocate**: `Intersects`, `Distance`, `Area` and `Bounds` are 0 B.

