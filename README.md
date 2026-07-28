# RxSpatial

The spatial libraries that have been developed by Microsoft, IBM and Oracle have substantially changed the capabilities of geospatial computing. These libraries implement several functionalities that include intersection, distance, and area for various geospatial objects. These libraries came out to address a wealth of use cases that were challenging in that era. As time goes by, GPS devices and location-aware mobile technologies increased the demand for geospatial computing, in general, and for real time geostreaming, in particular. Existing commercial spatial libraries were originally designed to support operations on stationary objects with limited or no capabilities for moving objects. In this work, we introduce the RxSpatial library, a real time reactive spatial library for spatiotemporal stream query processing. RxSpatial provides, (1) a front-end, which is a programming interface for developers who are familiar with the Microsoft .NET Reactive framework and the Microsoft SQL Server Spatial Library, and (2) a back-end for processing spatial operations in a streaming fashion. RxSpatial provides the programming convenience at the front end and the query processing efficiency at the back end.

## Citation

- Abdeltawab M. Hendawi, Youying Shi, Hossam Fattah, Jumana Karwa, Mohamed H. Ali. *RxSpatial: a framework for real-time spatio-temporal operations.* In ACM DEBS 2016, 366-367
- Youying Shi, Abdeltawab M. Hendawi, Hossam Fattah, Mohamed H. Ali. *RxSpatial: Reactive Spatial Library for Real-Time Location Tracking and Processing.* In ACM SIGMOD 2016, 2165-2168

A copy of the SIGMOD demo paper is included as [RxSpatialDemo.pdf](RxSpatialDemo.pdf).

## Solution layout

Open [`RxSpatial.slnx`](RxSpatial.slnx) at the repository root.

| Project | Description |
| --- | --- |
| `RxSpatial/RxSpatial` | The core library: `RxGeography` moving objects, continuous distance / intersection / intersect queries, and pluggable spatial index backends (list, grid, R-tree, RUM-tree). |
| `RTree` | Guttman R-tree, C# port of the Java version by Aled Morris (LGPL 2.1, see `RTree/LICENSE.txt`). |
| `RUMTree` | R-tree with Update Memo — reduces the cost of frequent location updates (same LGPL 2.1 lineage). |
| `RxSpatial/RxSpatial.Examples` | Runnable example / smoke test: Rx.NET v7 sequences feeding and consuming RxSpatial streams on every backend. |
| `RxSpatial/RxSpatial.Benchmarks` | BenchmarkDotNet suite: per-backend location-update cost, plus R-tree vs RUM-tree bulk-insert / update / query comparisons with allocation statistics. |
| `RxSpatial/MapRunRxSpatial` | WPF demo that visualises moving objects and continuous queries on a Bing Maps control. |

## Getting started

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Windows (the SQL Server spatial operations are implemented by the native `SqlServerSpatial170.dll` that ships in the `Microsoft.SqlServer.Types` NuGet package for win-x64, win-x86 and win-arm64).

```shell
dotnet build RxSpatial.slnx
dotnet run --project RxSpatial/RxSpatial.Examples
```

The examples program drives a vehicle towards a geofence around the Seattle Space Needle and asserts the behaviour of every spatial index backend; it doubles as an end-to-end smoke test.

To run the benchmarks (BenchmarkDotNet requires a Release build):

```shell
dotnet run --project RxSpatial/RxSpatial.Benchmarks -c Release -- --filter "*"
```

`SpatialBackendBenchmarks` measures the cost of pushing one location update through `RxGeography` per index backend — the list backend scales linearly with the observer count while the grid and tree backends prune by zone. `TreeIndexBenchmarks` compares the R-tree against the RUM-tree for bulk insertion, the frequent-update workload the RUM-tree was designed for, and intersection queries.

## Using RxSpatial with System.Reactive v7

`RxGeography` implements `IObserver<SqlGeography>` / `IObservable<SqlGeography>`, so it composes directly with Rx.NET. The library also ships `RxGeographyObservable` extension methods that expose the continuous query API as ordinary observable sequences:

```csharp
var fence = SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500);
var vehicle = new RxGeography();

// Consume continuous spatial queries with Rx operators...
using var alerts = vehicle.DistanceTo(fence)
                          .Select(d => (double)d)
                          .DistinctUntilChanged()
                          .Where(metres => metres < 1000)
                          .Subscribe(m => Console.WriteLine($"{m:F0} m from the fence"));

// ...and feed positions from any Rx sequence: RxGeography is an IObserver.
gpsFixes.Select(fix => SqlGeography.Point(fix.Lat, fix.Lon, 4326))
        .Subscribe(vehicle);
```

Continuous queries between two moving objects (`RxRx*` in the papers) are available through the same extensions: `a.DistanceTo(b)`, `a.Intersects(b)`, `a.IntersectionWith(b)`.

## Configuration

The library reads its settings from the host application's `App.config`:

| Key | Meaning | Default |
| --- | --- | --- |
| `preferredDataStruct` | Spatial index for observers: `list`, `hashmap` (grid), `RTREE`, or `RUMTREE`. | `list` |
| `RxSpatialQueueLimit` | Location-push buffer size before `RxSpatialOverLoadException` is thrown (buffered mode). | `1` |
| `isVisualized` | `true` processes pushes synchronously and forwards raw locations to direct subscribers (used by the WPF demo, the examples and the benchmarks); `false` is the buffered throughput mode. | `false` |
| `HashMapGridNum` / `HashMapCoverageRegion` | Grid backend dimensions and coverage rectangle (`lat1,long1,lat2,long2`). | `10,10` / `46,-124,49,-120` |
| `RUMTreeGCInspectionInterval` | RUM-tree garbage-cleaning token interval. | `10` |

## Modernisation notes (2026)

The codebase was originally built in 2014-2016 against .NET Framework 4.0/4.5 and no longer compiled from a fresh clone (machine-specific assembly hint paths, missing binaries, TFS bindings). It has been modernised as follows:

- **.NET 10 / SDK-style projects.** All projects target `net10.0` (`net10.0-windows` for the executables that need Windows facilities); the three per-project solutions and TFS source-control artefacts were replaced by a single `RxSpatial.slnx`, and build outputs, `packages/` folders and loose DLLs were removed from source control.
- **System.Reactive 7.0.0.** Rx was upgraded from the 2.2.5 `Rx-*` package family to Rx.NET v7, and the new `RxGeographyObservable` extensions (plus the `RxSpatial.Examples` project) make the library's streams first-class Rx sequences.
- **Microsoft.SqlServer.Types 170.1000.7.** The SQL Server 2025 generation of the official package supports modern .NET (netstandard2.0 + native win-x64/x86/arm64 runtimes), so the `SqlGeography` public API of the library is preserved exactly. Windows remains a runtime requirement.
- **log4net 3.3.2** and **System.Configuration.ConfigurationManager 10.0.10** replace the loose `log4net.dll` and the Framework `System.Configuration` assembly. Runtime switching of `preferredDataStruct` through `ConfigurationManager.AppSettings` (as done by the load-test harness) still works.
- **Bing Maps WPF control.** `MapRunRxSpatial` now consumes `Microsoft.Maps.MapControl.WPF` 1.0.1 from NuGet (a .NET Framework assembly that runs under .NET via `AssetTargetFallback`; the app launches and runs). Note that Bing Maps for Enterprise was retired in June 2025, so the embedded map credentials may no longer serve tiles; the control and the demo logic remain functional.
- **BenchmarkDotNet.** The original `LoadTestApplication` and `memoryTestApplication` console harnesses (together with the 2016 experiment outputs they had written into their source folders) were replaced by the `RxSpatial.Benchmarks` BenchmarkDotNet 0.15.8 suite, which reproduces both measurements — per-backend update-processing cost and R-tree vs RUM-tree behaviour — with proper statistics and allocation diagnostics. The originals remain available in the git history.
- **Data paths.** `MapRunRxSpatial` now resolves its traffic data relative to the build output (files are copied on build) instead of `../../`, so it runs from a fresh clone.
- Alternative backend prototypes that were never part of the compiled 2016 assemblies (`RxGeography_.cs`, `RxGeographyGrid/RTree/RUMTree/SqlSpatial/Tree.cs`, `MovingObj.cs`, `IntersctObserver.cs`, `Observer.cs`) are kept as reference source but remain excluded from compilation, so the built API surface matches the original. The vendored R-tree/RUM-tree ports are intentionally untouched apart from retargeting; their historical compiler warnings (including two `#warning` port markers) are left visible.
