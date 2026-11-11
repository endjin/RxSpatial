# RxSpatial.Geo

The shapes and shape maths that [RxSpatial](https://github.com/endjin/RxSpatial) uses. You can also use it without RxSpatial, to test whether shapes intersect, measure the distance between them, or compute the area they share. It has no dependencies and runs wherever .NET runs.

```csharp
using RxSpatial.Geo;

var needle = new GeoPoint(47.6205, -122.3493);
var fence = new GeoCircle(needle, radiusMetres: 500);
var downtown = GeoPolygon.Rectangle(south: 47.600, west: -122.370, north: 47.618, east: -122.330);

bool inside = GeoOperations.Intersects(fence, new GeoPointShape(47.6230, -122.3480));   // true
double metres = GeoOperations.Distance(fence, downtown);                                 // 0: they overlap
GeoShape overlap = GeoOperations.Intersection(fence, downtown);                          // a convex GeoPolygon
double squareMetres = GeoOperations.Area(overlap);                                       // the exact curved area
GeoPoint[] outline = GeoOperations.ToVertices(fence);                                    // 128 points on the circle
```

## Two families of shapes

| Geodetic (WGS84, metres) | Planar (flat X/Y, your units) |
| --- | --- |
| `GeoPointShape` | `PlanarPointShape` |
| `GeoCircle` | `PlanarCircle` |
| `GeoPolygon` | `PlanarPolygon` |
| `GeoEmpty` | `PlanarEmpty` |
| `GeoOperations` | `PlanarOperations` |

`GeoOperations` and `PlanarOperations` hold the maths: `Intersects`, `Intersection`, `Distance`, `Area`, `Bounds` and `ToVertices`.

```csharp
var bay = PlanarPolygon.Rectangle(minX: 450, minY: 450, maxX: 550, maxY: 550);
double distance = PlanarOperations.Distance(bay, new PlanarPointShape(400, 500));   // 50
```

## Things to know

- Geodesics are Vincenty's inverse and direct solutions on the WGS84 ellipsoid. A nearly antipodal pair of points may not converge; the distance is then `double.NaN`.
- Circles are exact, not polygons.
- Polygons must be convex. A concave or self-intersecting ring throws `ArgumentException`.
- Geodetic shapes are at most 500 km across (`GeoShape.MaxExtent`); a circle's radius is at most 250 km.
- The antimeridian is not split: bounds do not wrap at ±180°.
- `Intersects`, `Distance`, `Area` and `Bounds` do not allocate.

## More

- [Using RxSpatial](https://github.com/endjin/RxSpatial/blob/main/docs/usage.md#shapes-rxspatialgeo): every shape type, how the maths works and its limits.
- [SQL Server types and fidelity](https://github.com/endjin/RxSpatial/blob/main/docs/sql-server.md): how closely the results match SQL Server's spatial types.
- [The repository](https://github.com/endjin/RxSpatial).

Released under the MIT licence.
