# RxSpatial

RxSpatial is a .NET library for asking questions about things that move, and getting the answer again every time they move. Is this vehicle inside that zone? How far is it from the depot? Which two vehicles are close enough to share a ride?

You do not poll for the answer. Each moving object is an Rx.NET observer of its own position and an observable source of query results: push a new position in, and every query that involves the object pushes its new answer out.

[![The MapRun RxSpatial demo on the Geofence tab. The control panel on the left has the traffic controls, the area drawing buttons, the spatial index switch and the guidance for the tab. The map of Seattle on the right shows a purple dashed confinement area, a hatched restricted area and 20 numbered vehicles. Vehicles inside the restricted area or outside the confinement area are red diamonds, and the others are blue circles.](https://res.cloudinary.com/endjin/image/upload/v1792916244/assets/images/blog/2026/10/maprun-rxspatial.jpg)](https://youtu.be/1NCQoFl3x_c)

[Watch the two-minute demo video on YouTube](https://youtu.be/1NCQoFl3x_c)

RxSpatial began as a research project, published at ACM SIGMOD, DEBS and SIGSPATIAL in 2016 and IEEE ICDE in 2017 (see [Research and citation](#research-and-citation)). endjin modernised the 2016 code in 2026: it now targets .NET 10 and System.Reactive 7, and the core runs on Windows, Linux and macOS with no native or SQL Server dependency.

## A first example

A vehicle reports its GPS position. We want to know when it comes within 1 km of a 500 m fence around the Seattle Space Needle.

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

`DistanceTo(fence)` is a continuous query: it produces one distance for every position the vehicle reports. Everything after it is ordinary Rx.NET.

## What you can ask

There are three questions, and each can be asked about a moving object and a fixed area, or about two moving objects. The papers name the first family with an `Rx` prefix and the second with `RxRx`.

| Question | Moving object and a fixed area | Two moving objects | Each answer is |
| --- | --- | --- | --- |
| Do they intersect? | `RxIntersect`, or `a.Intersects(area)` | `RxRxIntersect`, or `a.Intersects(b)` | `bool` |
| How far apart are they? | `RxDistance`, or `a.DistanceTo(area)` | `RxRxDistance`, or `a.DistanceTo(b)` | `double` |
| What area do they share? | `RxIntersection`, or `a.IntersectionWith(area)` | `RxRxIntersection`, or `a.IntersectionWith(b)` | a shape |

The `Rx*` methods take an `IObserver`, as in the papers. The extension methods return the same queries as `IObservable` sequences, as in the example above.

A moving object is one of two classes:

- **`RxGeography`** works in latitude and longitude on the WGS84 ellipsoid. Distances are in metres. Its shapes are points, circles and convex polygons.
- **`RxGeometry`** works in flat X/Y coordinates, for a warehouse floor, a CAD drawing or a projected map. Distances are in your own units.

The two cannot be mixed: asking an `RxGeometry` about an `RxGeography` is a compile error.

## Getting started

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

On Windows, build, test and run the examples:

```shell
dotnet build solutions/RxSpatial.slnx
dotnet test --solution solutions/RxSpatial.slnx
dotnet run --project solutions/RxSpatial.Examples
```

On Linux or macOS, use the cross-platform solution filter, because the solution also holds Windows-only projects:

```shell
dotnet build solutions/RxSpatial.CrossPlatform.slnf
dotnet test --solution solutions/RxSpatial.CrossPlatform.slnf
dotnet run --project solutions/RxSpatial.Examples
```

The examples program drives a vehicle towards a geofence around the Seattle Space Needle, and a planar `RxGeometry` forklift towards a loading bay, and asserts the behaviour of every spatial index backend. It doubles as an end-to-end smoke test and ends with `All scenarios passed.`

On Windows you can also run the map demo shown above:

```shell
dotnet run --project solutions/MapRunRxSpatial
```

It replays 20 recorded vehicle trajectories around Seattle, with one tab per scenario: geofences, distance to an area, distance between vehicles, convoy links and overlapping reaches. It needs the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (preinstalled on Windows 11) and internet access for the map. See [The map demo](docs/map-demo.md).

RxSpatial is not released as a NuGet package. To use it in your own code, reference `solutions/RxSpatial/RxSpatial.csproj`.

## What is in the repository

| Project | Runs on | What it is |
| --- | --- | --- |
| `RxSpatial` | Windows, Linux, macOS | The core library: `RxGeography`, `RxGeometry` and the continuous queries. |
| `RxSpatial.Geo` | Windows, Linux, macOS | The shapes and their maths: intersects, intersection, distance, area and bounds. No dependencies. |
| `RxSpatial.Index` | Windows, Linux, macOS | The R*-tree and RUM-tree spatial indexes. No dependencies. |
| `RxSpatial.Examples` | Windows, Linux, macOS | The runnable examples and smoke test. |
| `RxSpatial.Tests` | Windows, Linux, macOS | The tests for the three libraries. |
| `RxSpatial.Benchmarks`, `RxSpatial.Experiments` | Windows, Linux, macOS | BenchmarkDotNet suites, and a harness that reproduces the ICDE 2017 experiments. |
| `MapRunRxSpatial` | Windows | The WPF map demo. |
| `archive/RxSpatial.SqlServer` | Windows | The same moving objects over SQL Server's `SqlGeography` and `SqlGeometry` types, kept outside the main solution. |

Every project is a folder under `solutions/`. [Repository layout](docs/repository-layout.md) describes each one in full.

## Things to know before you rely on it

- **Polygons are convex only.** A concave or self-intersecting ring throws `ArgumentException`. If you need concave or multi-part shapes, use the archived [SQL Server types](docs/sql-server.md) on Windows.
- **Geodetic shapes are at most 500 km across.** A circle's radius is at most 250 km.
- **A distance can be `double.NaN`.** This happens for an empty shape and for a nearly antipodal pair of points. NaN fails every comparison, so filter with `double.IsNaN` if your pipeline must not see it.
- **The spatial index is a setting.** A moving object finds the queries a position affects through a list (the default), a grid, an R*-tree or a RUM-tree. The answers are the same on each; the cost per update is not. The list scales linearly with the number of observers, while the grid and trees prune by zone. See [Configuration](docs/configuration.md) and [Benchmarks](docs/benchmarks.md).
- **Empty shapes need the list index.** An empty shape has no bounds, so pushing one to the grid or tree indexes throws `InvalidOperationException`.

## Documentation

| Read this | To find out |
| --- | --- |
| [Using RxSpatial](docs/usage.md) | The query API in full, `RxGeometry`, the shape types, how the maths works and its limits. |
| [Configuration](docs/configuration.md) | `RxSpatialOptions`: the index, the push queue, the grid, and binding from `appsettings.json`. |
| [The map demo](docs/map-demo.md) | What each tab shows, the colours, and how the defaults follow the papers' figures. |
| [SQL Server types and fidelity](docs/sql-server.md) | `RxSqlGeography` and `RxSqlGeometry`, and how closely the core matches SQL Server's results. |
| [Benchmarks](docs/benchmarks.md) | The cost of an update on each index, each shape operation, and multi-core scaling. |
| [Reproducing the ICDE 2017 experiments](docs/icde-experiments.md) | The latency and load experiments from the paper, and how the results compare today. |
| [Performance](docs/performance.md) | The profiling-led performance and memory work, with before and after measurements. |
| [Build and CI](docs/build-and-ci.md) | The `build.ps1` build, the GitHub Actions workflows and the repository settings they need. |
| [Repository layout](docs/repository-layout.md) | Every project and where it lives. |
| [CHANGELOG](CHANGELOG.md) | Every change since the 2016 code, including the breaking changes and the modernisation notes. |

## Research and citation

The original authors introduced RxSpatial like this:

> The spatial libraries that have been developed by Microsoft, IBM and Oracle have substantially changed the capabilities of geospatial computing. These libraries implement several functionalities that include intersection, distance, and area for various geospatial objects. These libraries came out to address a wealth of use cases that were challenging in that era. As time goes by, GPS devices and location-aware mobile technologies increased the demand for geospatial computing, in general, and for real time geostreaming, in particular. Existing commercial spatial libraries were originally designed to support operations on stationary objects with limited or no capabilities for moving objects. In this work, we introduce the RxSpatial library, a real time reactive spatial library for spatiotemporal stream query processing. RxSpatial provides, (1) a front-end, which is a programming interface for developers who are familiar with the Microsoft .NET Reactive framework and the Microsoft SQL Server Spatial Library, and (2) a back-end for processing spatial operations in a streaming fashion. RxSpatial provides the programming convenience at the front end and the query processing efficiency at the back end.

To cite it:

- Abdeltawab M. Hendawi, Youying Shi, Hossam Fattah, Jumana Karwa, Mohamed H. Ali. *RxSpatial: a framework for real-time spatio-temporal operations.* In ACM DEBS 2016, 366-367
- Youying Shi, Abdeltawab M. Hendawi, Hossam Fattah, Mohamed H. Ali. *RxSpatial: Reactive Spatial Library for Real-Time Location Tracking and Processing.* In ACM SIGMOD 2016, 2165-2168
- Youying Shi, Abdeltawab M. Hendawi, Jayant Gupta, Hossam Fattah, Mohamed H. Ali. *RxSpatial: the reactive spatial library.* In ACM SIGSPATIAL GIS 2016, 64
- Abdeltawab M. Hendawi, Jayant Gupta, Youying Shi, Hossam Fattah, Mohamed H. Ali. *The Microsoft Reactive Framework Meets the Internet of Moving Things.* In IEEE ICDE 2017, 1150-1161 ([doi:10.1109/ICDE.2017.159](https://doi.org/10.1109/ICDE.2017.159)) — the full paper; the map demo's scenarios and default settings follow its figures

A copy of the SIGMOD demo paper is included as [docs/RxSpatialDemo.pdf](docs/RxSpatialDemo.pdf).

## Licence

RxSpatial is released under the [MIT License](LICENSE), the licence that the original authors added to [williamshyy/RxSpatial](https://github.com/williamshyy/RxSpatial) in October 2026. The contributors are listed in [AUTHORS.txt](AUTHORS.txt). The NuGet packages that RxSpatial references keep their own licences (see the Microsoft.SqlServer.Types note under [SQL Server types](docs/sql-server.md)). The upstream repository also contains R-tree and RUM-tree ports under the GNU LGPL v2.1. RxSpatial does not use them: `RxSpatial.Index` is a clean-room replacement, and the ports remain only in this repository's git history.
