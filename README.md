# RxSpatial

The spatial libraries that have been developed by Microsoft, IBM and Oracle have substantially changed the capabilities of geospatial computing. These libraries implement several functionalities that include intersection, distance, and area for various geospatial objects. These libraries came out to address a wealth of use cases that were challenging in that era. As time goes by, GPS devices and location-aware mobile technologies increased the demand for geospatial computing, in general, and for real time geostreaming, in particular. Existing commercial spatial libraries were originally designed to support operations on stationary objects with limited or no capabilities for moving objects. In this work, we introduce the RxSpatial library, a real time reactive spatial library for spatiotemporal stream query processing. RxSpatial provides, (1) a front-end, which is a programming interface for developers who are familiar with the Microsoft .NET Reactive framework and the Microsoft SQL Server Spatial Library, and (2) a back-end for processing spatial operations in a streaming fashion. RxSpatial provides the programming convenience at the front end and the query processing efficiency at the back end.

## Citation

- Abdeltawab M. Hendawi, Youying Shi, Hossam Fattah, Jumana Karwa, Mohamed H. Ali. *RxSpatial: a framework for real-time spatio-temporal operations.* In ACM DEBS 2016, 366-367
- Youying Shi, Abdeltawab M. Hendawi, Hossam Fattah, Mohamed H. Ali. *RxSpatial: Reactive Spatial Library for Real-Time Location Tracking and Processing.* In ACM SIGMOD 2016, 2165-2168

A copy of the SIGMOD demo paper is included as [docs/RxSpatialDemo.pdf](docs/RxSpatialDemo.pdf).

## Repository layout

Open [`RxSpatial.slnx`](RxSpatial.slnx) at the repository root.

| Path | Description |
| --- | --- |
| `src/RxSpatial` | The core library: `RxGeography` moving objects, continuous distance / intersection / intersect queries, and pluggable spatial index backends (list, grid, R-tree, RUM-tree). |
| `src/RTree` | Guttman R-tree, vendored C# port of the Java version by Aled Morris (LGPL 2.1, see `src/RTree/LICENSE.txt`); sources intentionally kept in their original style. |
| `src/RUMTree` | R-tree with Update Memo — reduces the cost of frequent location updates (same LGPL 2.1 lineage). |
| `samples/RxSpatial.Examples` | Runnable example / smoke test: Rx.NET v7 sequences feeding and consuming RxSpatial streams on every backend. |
| `samples/MapRunRxSpatial` | WPF demo that visualises moving objects and continuous queries on a Bing Maps control. |
| `benchmarks/RxSpatial.Benchmarks` | BenchmarkDotNet suite: per-backend location-update cost, plus R-tree vs RUM-tree bulk-insert / update / query comparisons with allocation statistics. |
| `tests/RxSpatial.Tests` | MSTest (MSTest.Sdk + Shouldly) behavioural tests for the library and the tree indexes. |
| `docs/prototypes` | Never-compiled 2016 prototype sources, archived with provenance notes. |

## Getting started

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Windows (the SQL Server spatial operations are implemented by the native `SqlServerSpatial170.dll` that ships in the `Microsoft.SqlServer.Types` NuGet package for win-x64, win-x86 and win-arm64).

```shell
dotnet build RxSpatial.slnx
dotnet test
dotnet run --project samples/RxSpatial.Examples
```

The examples program drives a vehicle towards a geofence around the Seattle Space Needle and asserts the behaviour of every spatial index backend; it doubles as an end-to-end smoke test. The same scenarios are covered as unit tests in `tests/RxSpatial.Tests`.

To run the benchmarks (BenchmarkDotNet requires a Release build):

```shell
dotnet run --project benchmarks/RxSpatial.Benchmarks -c Release -- --filter "*"
```

`SpatialBackendBenchmarks` measures the cost of pushing one location update through `RxGeography` per index backend — the list backend scales linearly with the observer count while the grid and tree backends prune by zone. `TreeIndexBenchmarks` compares the R-tree (the per-category baseline) against the RUM-tree for bulk insertion, the frequent-update workload the RUM-tree was designed for, and intersection queries.

## Using RxSpatial with System.Reactive v7

`RxGeography` implements `IObserver<SqlGeography>` / `IObservable<SqlGeography>`, so it composes directly with Rx.NET. The library also ships `RxGeographyObservable` — C# 14 extension members that expose the continuous query API as ordinary observable sequences:

```csharp
var fence = SqlGeography.Point(47.6205, -122.3493, 4326).STBuffer(500);
var vehicle = new RxGeography();

// Consume continuous spatial queries with Rx operators...
using var alerts = vehicle.DistanceTo(fence)
                          .Select(d => (double)d)
                          .DistinctUntilChanged()
                          .Where(metres => metres < 1000)
                          .Subscribe(m => Console.WriteLine($"{m:F0} m from the fence"));

// ...observe the raw position stream...
using var track = vehicle.Locations.Subscribe(p => Plot(p));

// ...and feed positions from any Rx sequence: RxGeography is an IObserver.
gpsFixes.Select(fix => SqlGeography.Point(fix.Lat, fix.Lon, 4326))
        .Subscribe(vehicle);
```

Continuous queries between two moving objects (`RxRx*` in the papers) are available through the same extensions: `a.DistanceTo(b)`, `a.Intersects(b)`, `a.IntersectionWith(b)`.

## Configuration

Settings are typed (`RxSpatialOptions`) and can be passed straight to the constructor:

```csharp
var vehicle = new RxGeography(new RxSpatialOptions
{
    PreferredDataStruct = RxGeographyDataStruct.RTree,
    IsVisualized = true,
});
```

or bound from any Microsoft.Extensions.Configuration source via the `RxSpatial` section — the parameterless `RxGeography()` constructor uses the ambient `RxSpatialOptions.Default`, typically set once at start-up:

```csharp
IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "RXSPATIAL_")
    .Build();

RxSpatialOptions.Default =
    configuration.GetSection(RxSpatialOptions.SectionName).Get<RxSpatialOptions>() ?? new();
```

```json
{
  "RxSpatial": {
    "PreferredDataStruct": "RTree",
    "QueueLimit": 3,
    "IsVisualized": true,
    "Grid": {
      "LatitudeCells": 10,
      "LongitudeCells": 10,
      "CoverageRegion": {
        "LowerLeftLatitude": 46, "LowerLeftLongitude": -124,
        "UpperRightLatitude": 49, "UpperRightLongitude": -120
      }
    }
  }
}
```

| Option | Meaning | Default |
| --- | --- | --- |
| `PreferredDataStruct` | Spatial index for observers: `List`, `HashMap` (grid), `RTree`, or `RumTree`. Historic setting values (`RTREE`, `hashmap`, ...) bind case-insensitively. | `List` |
| `QueueLimit` | Location-push buffer size before `RxSpatialOverLoadException` is thrown (buffered mode). | `1` |
| `IsVisualized` | `true` processes pushes synchronously and forwards raw locations to direct subscribers (used by the WPF demo, the examples and the benchmarks); `false` is the buffered throughput mode. | `false` |
| `Grid` | Grid backend dimensions and coverage rectangle. | `10×10` over `46,-124 .. 49,-120` |

One legacy exception: the vendored RUM-tree's lazy garbage cleaner still reads the `RUMTreeGCInspectionInterval` key from classic `App.config` appSettings (default `10`), because those LGPL sources are intentionally untouched.

## Modernisation notes (2026)

The codebase was originally built in 2014-2016 against .NET Framework 4.0/4.5 and no longer compiled from a fresh clone (machine-specific assembly hint paths, missing binaries, TFS bindings). It has been modernised in two passes — first a faithful migration, then a restructure to current standards:

- **.NET 10, C# 14 and SDK-style projects** throughout, arranged in the standard `src` / `samples` / `benchmarks` / `tests` / `docs` layout with a single `RxSpatial.slnx`, shared `Directory.Build.props` (nullable, implicit usings, artifacts output, warnings-as-errors in Release), central package management via `Directory.Packages.props`, `.editorconfig`, pinned `global.json` and a GitHub Actions workflow.
- **System.Reactive 7.0.0.** Rx upgraded from the 2.2.5 `Rx-*` package family; the `RxGeographyObservable` C# 14 extension members make the library's streams first-class Rx sequences (see above).
- **Microsoft.SqlServer.Types 170.1000.7.** The SQL Server 2025 generation of the official package supports modern .NET (netstandard2.0 + native win-x64/x86/arm64 runtimes), so the `SqlGeography` public API of the library is preserved exactly. Windows remains a runtime requirement.
- **Tests and benchmarks.** The original `LoadTestApplication` and `memoryTestApplication` consoles (and the 2016 experiment outputs they had written into their source folders) were replaced by the `RxSpatial.Benchmarks` BenchmarkDotNet suite; behavioural coverage lives in `tests/RxSpatial.Tests` (MSTest.Sdk). The originals remain in the git history.
- **Modern configuration.** The legacy `System.Configuration.ConfigurationManager` appSettings were replaced by typed `RxSpatialOptions` with constructor injection plus an ambient default bound from Microsoft.Extensions.Configuration (see Configuration above); the samples now use `appsettings.json` instead of `App.config`. The core library itself has no configuration package dependency at all.
- **Core library refactor.** File-scoped namespaces, nullable annotations that preserve the original null-flow, dead code removed, and a handful of verified bug fixes: the grid backend's non-square-grid maths (`longStep` and the cell-key stride), the grid's dead point-handling branch (`SqlString`-vs-`string` comparison that could never be true), and tree-backend unsubscription (`Remove`/`Contains` previously consulted the wrong index map, so disposing a subscription never detached observers from an R-tree backend).
- **Vendored tree ports untouched.** `src/RTree` and `src/RUMTree` are LGPL 2.1 vendored source kept byte-identical apart from project files; their historic compiler warnings remain visible and are exempt from warnings-as-errors.
- **Bing Maps WPF control.** `samples/MapRunRxSpatial` consumes `Microsoft.Maps.MapControl.WPF` 1.0.1 from NuGet (a .NET Framework assembly running under .NET via `AssetTargetFallback`; the app launches and runs). Bing Maps for Enterprise was retired in June 2025, so the embedded map credentials may no longer serve tiles; the control and the demo logic remain functional.
