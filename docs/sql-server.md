# SQL Server types and fidelity

## SQL Server types: `RxSpatial.SqlServer`

To keep `SqlGeography` and `SqlGeometry` — concave or multi-part fences, SRIDs, or results that must match SQL Server exactly — reference `solutions/archive/RxSpatial.SqlServer` and rename the classes. `RxSqlGeography` and `RxSqlGeometry` are the previous `RxGeography` and `RxGeometry`, with the same members, the same behaviour (including the Null-to-NaN distance rule) and the same `RxSqlGeographyObservable` / `RxSqlGeometryObservable` extensions. They run on the same engine and index backends as the core.

```csharp
using Microsoft.SqlServer.Types;
using RxSpatial.SqlServer;

var fence = SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500);
var vehicle = new RxSqlGeography();          // was: new RxGeography()
using var alerts = vehicle.Intersects(fence).Subscribe(inside => Console.WriteLine(inside));
vehicle.OnNext(SqlGeography.Point(47.6230, -122.3480, 4326));
```

The package is Windows-only (`[SupportedOSPlatform("windows")]`), because Microsoft.SqlServer.Types runs its spatial operations in a native Windows library.

**Licence note.** The current Microsoft.SqlServer.Types release, 170.1000.7 (the SQL Server 2025 generation, and the first with modern .NET support), ships under Microsoft's *pre-release software licence terms* for a SQL Server Community Technology Preview, which restrict use in a live operating environment. Read those terms before you deploy `RxSpatial.SqlServer`. The cross-platform core does not use the package at all; only `RxSpatial.SqlServer`, its tests and its benchmarks do.

## Fidelity against SQL Server

`solutions/RxSpatial.Tests/Golden/geodetic.json` (170 cases) and `planar.json` (31 cases) hold SQL Server's own results for Seattle-scale inputs: the Space Needle route against a 500 m fence, point pairs from 10 m to 50 km, 2 km lenses at 0.5–3.9 km separation, circles against rectangles, rectangles against rectangles, intersects at the radius ±δ in eight directions, and disc–disc and disc–rectangle distances. `GoldenFidelityTests` checks RxSpatial.Geo against them on every platform; on Windows, the archived `GoldenValueGuardTests` checks, when run by hand, that live SQL Server still gives the recorded values. The budget, and the worst difference measured (geodetic):

| Quantity                                               | Budget                                    | Worst measured                                          |
|--------------------------------------------------------|-------------------------------------------|---------------------------------------------------------|
| Point–point distance                                   | max(0.01 %, 1 cm)                         | 0.26 mm (30 cases)                                      |
| Point–circle distance                                  | 0.5 m                                     | 0.14 m (103 cases) ¹                                    |
| Point–polygon distance                                 | 0.5 m                                     | 0.75 mm                                                 |
| Circle–circle / circle–polygon distance                | 0.5 m plus the chord sag of each circle ¹ | 1.09 m / 0.51 m, both within budget                     |
| Areas (circle, polygon, lens, clipped circle)          | 0.15 %                                    | 0.13 % (a 3 km lens); circles 0.04 %, polygons 0.0001 % |
| Thin lenses (< 1 % of the disc)                        | 0.15 % of the analytic area               | 1e-10                                                   |
| Intersects, more than 0.5 m (plus sag) from a boundary | must agree                                | agrees in every case                                    |

¹ SQL Server's `STBuffer` disc is an inscribed 128-gon, whose chords sag r(1 − cos(π/128)) ≈ 0.03 % of the radius inside the true circle (0.15 m for a 500 m fence, 0.6 m for a 2 km disc). The differences above are mostly that sag: RxSpatial.Geo measures to the exact circle. The planar maths matches SQL Server exactly (within 1e-13) wherever SQL Server is exact, that is, without circles.

