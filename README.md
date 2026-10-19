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
| `src/RxSpatial` | The core library: `RxGeography` moving objects, continuous distance / intersection / intersect queries, and pluggable spatial index backends (list, grid, R*-tree, RUM-tree). |
| `src/RxSpatial.Index` | R*-tree (Beckmann et al., 1990) and RUM-tree (R-tree with Update Memo, Xiong &amp; Aref) spatial indexes sharing one engine; a clean-room .NET 10 replacement for the formerly vendored LGPL Java ports. Zero dependencies. |
| `samples/RxSpatial.Examples` | Runnable example / smoke test: Rx.NET v7 sequences feeding and consuming RxSpatial streams on every backend. |
| `samples/MapRunRxSpatial` | WPF demo that visualises moving objects and continuous queries on a MapLibre GL JS map hosted in WebView2 (OpenStreetMap tiles, no API key). |
| `benchmarks/RxSpatial.Benchmarks` | BenchmarkDotNet suite: per-backend location-update cost, plus R*-tree vs RUM-tree bulk-insert / update / query comparisons with allocation statistics. |
| `tests/RxSpatial.Tests` | MSTest (MSTest.Sdk + Shouldly) behavioural tests for the library and the tree indexes. |
| `docs/prototypes` | Never-compiled 2016 prototype sources, archived with provenance notes. |

## Getting started

Requirements: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Windows (the SQL Server spatial operations are implemented by the native `SqlServerSpatial170.dll` that ships in the `Microsoft.SqlServer.Types` NuGet package for win-x64, win-x86 and win-arm64). The WPF map demo additionally needs the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (preinstalled on Windows 11) and internet access for the MapLibre GL JS CDN and OpenStreetMap tiles.

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

`SpatialBackendBenchmarks` measures the cost of pushing one location update through `RxGeography` per index backend — the list backend scales linearly with the observer count while the grid and tree backends prune by zone. `TreeIndexBenchmarks` compares the R*-tree (the per-category baseline) against the RUM-tree for bulk insertion, the frequent-update workload the RUM-tree was designed for, and intersection queries (including the allocation-free buffered `Search` overload). Because both trees share the same R* engine, the measured delta is purely the update-memo strategy. After profile-guided optimisation of the engine (a containment fast path in ChooseSubtree found via dotnet-trace, and pooled reinsertion scratch found via dotnet-dump heap analysis), the paper's thesis holds in memory too: RUM-tree updates run at roughly 0.6× the cost of an R* remove-and-reinsert (~0.6 µs vs ~1.0 µs at N=1000, and ~375× the original vendored port's 229 µs) with near-zero steady-state allocation. Coverage-driven `Churn` (delete + reinsert) and `UpdateLocal` (small-delta tracking moves) categories then motivated a bottom-up fast path: when a moved entry still fits its leaf's recorded cover it is rewritten in place, dropping local moves to ~0.42× the cost of a teleporting update with ~zero allocation — so the R*-tree now wins tracking workloads while the RUM-tree wins teleport-heavy ones.

### Running the map demo

```shell
dotnet run --project samples/MapRunRxSpatial
```

The control panel has one tab per scenario — **Inside area** (`RxIntersect`), **Near area** (`RxDistance`), **Near vehicle** (`RxRxDistance`) and **Convoy link** (`RxRxDistance` with a connection line) — and each tab shows its own steps and colour legend. *Inside area* uses the spatial index configured in `appsettings.json` (the **RUM-tree by default**); the other tabs always use the List index, because spatial pruning would suppress their continuous distance streams. The panel footer shows the index in use.

Each tab starts with the settings of the matching figure in the RxSpatial papers, so pressing **Start** reproduces it. Nothing moves until you press **Start**, and switching tabs restores these defaults:

| Tab | Vehicles | Observe distance | Watched pairs | Reproduces |
| --- | --- | --- | --- | --- |
| *Inside area* | 20 | — | — | DEBS 2016 Fig. 5 |
| *Near area* | 20 | 4,600 m | — | DEBS 2016 Fig. 4 (family locator, 4,610.93 m) |
| *Near vehicle* | 20 (fixed) | 5,000 m | 10–12, 15–16 | SIGMOD 2016 control panel (5,065.43 m); ICDE Fig. 4c pairs |
| *Convoy link* | 20 (fixed) | 8,000 m | 4–18, 10–12, 7–9 | ICDE Fig. 5 / DEBS 2016 Fig. 3 |

**Replay rate** is 25 updates/s on every tab. The papers do not give a consistent value (their screenshots show 1, 48 and 56 on a differently scaled slider), so this is a readable middle value. The papers give no area coordinates, so on the area tabs press **Add area** and click two corners on the map. The pre-set pairs are watched as soon as their vehicles appear after **Start**. To watch more pairs, choose a **First vehicle** and a **Second vehicle** and press **Watch pair**. **Clear pairs** removes every pair, the pre-set pairs too, until the next tab switch.

The *Convoy link* figure shows 9,935 m. On the recorded trajectories, pair 4–18 is beyond that distance only for the first few updates, so it would turn green almost at once. At 8,000 m, the pairs stay in the states the figure describes for most of the replay: 4–18 is gray (beyond the limit) 53% of the time, 10–12 is yellow (stable) 94% of the time, and 7–9 is green (near the limit) more often than any other colour (40%).

Colours, where *d* is the observe distance (a vehicle in several queries shows its most urgent colour):

| Colour | Area tabs (vehicle vs area) | Pair tabs (vehicle vs vehicle; *Convoy link* line) |
| --- | --- | --- |
| Blue | Not inside an area, or farther than *d* from one | Not watched |
| Gray | — | Watched, farther apart than *d* (dark dotted line) |
| Green | Within *d* | Within *d*, near the limit (thin line) |
| Yellow | Closer than 4/7 *d* | Closer than 4/7 *d*: stable working distance (thick line) |
| Red | Inside an area | Closer than 1/7 *d*: the minimum safe distance is breached (thickest line) |

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

| Option                    | Meaning                                                                                                                                                                                  | Default                           |
|---------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------|
| `PreferredDataStruct`     | Spatial index for observers: `List`, `HashMap` (grid), `RTree` (R*-tree), or `RumTree`. Historic setting values (`RTREE`, `hashmap`, ...) bind case-insensitively.                       | `List`                            |
| `RumTreeCleaningInterval` | Cleaning-token interval of the RUM-tree backend: garbage-creating operations between purges of one leaf (successor to the legacy `RUMTreeGCInspectionInterval` appSetting).              | `5`                               |
| `QueueLimit`              | Location-push buffer size before `RxSpatialOverloadException` is thrown (buffered mode).                                                                                                 | `1`                               |
| `IsVisualized`            | `true` processes pushes synchronously and forwards raw locations to direct subscribers (used by the WPF demo, the examples and the benchmarks); `false` is the buffered throughput mode. | `false`                           |
| `Grid`                    | Grid backend dimensions and coverage rectangle.                                                                                                                                          | `10×10` over `46,-124 .. 49,-120` |


## Modernisation notes (2026)

The codebase was originally built in 2014-2016 against .NET Framework 4.0/4.5 and no longer compiled from a fresh clone (machine-specific assembly hint paths, missing binaries, TFS bindings). It has been modernised in two passes — first a faithful migration, then a restructure to current standards:

- **.NET 10, C# 14 and SDK-style projects** throughout, arranged in the standard `src` / `samples` / `benchmarks` / `tests` / `docs` layout with a single `RxSpatial.slnx`, shared `Directory.Build.props` (nullable, implicit usings, artifacts output, warnings-as-errors in Release), central package management via `Directory.Packages.props`, `.editorconfig`, pinned `global.json` and a GitHub Actions workflow.
- **System.Reactive 7.0.0.** Rx upgraded from the 2.2.5 `Rx-*` package family; the `RxGeographyObservable` C# 14 extension members make the library's streams first-class Rx sequences (see above).
- **Microsoft.SqlServer.Types 170.1000.7.** The SQL Server 2025 generation of the official package supports modern .NET (netstandard2.0 + native win-x64/x86/arm64 runtimes), so the `SqlGeography` public API of the library is preserved exactly. Windows remains a runtime requirement.
- **Tests and benchmarks.** The original `LoadTestApplication` and `memoryTestApplication` consoles (and the 2016 experiment outputs they had written into their source folders) were replaced by the `RxSpatial.Benchmarks` BenchmarkDotNet suite; behavioural coverage lives in `tests/RxSpatial.Tests` (MSTest.Sdk). The originals remain in the git history.
- **Modern configuration.** The legacy `System.Configuration.ConfigurationManager` appSettings were replaced by typed `RxSpatialOptions` with constructor injection plus an ambient default bound from Microsoft.Extensions.Configuration (see Configuration above); the samples now use `appsettings.json` instead of `App.config`. The core library itself has no configuration package dependency at all.
- **Core library refactor.** File-scoped namespaces, nullable annotations that preserve the original null-flow, dead code removed, and a handful of verified bug fixes: the grid backend's non-square-grid maths (`longStep` and the cell-key stride), the grid's dead point-handling branch (`SqlString`-vs-`string` comparison that could never be true), and tree-backend unsubscription (`Remove`/`Contains` previously consulted the wrong index map, so disposing a subscription never detached observers from an R-tree backend).
- **Modern spatial indexes.** The 20-year-old vendored Java ports of the R-tree and RUM-tree (4,068 lines, LGPL 2.1, log4net-era) were replaced by `src/RxSpatial.Index`: a clean-room R*-tree engine shared by both trees, with the RUM-tree implemented per the paper (long stamps, per-item update memo, tombstones, a round-robin cleaning token over a leaf ring). The item-keyed API (`Insert`/`Update`/`Remove`/`Search`) made the core library's per-tree rectangle caches unnecessary, `Count` now reports live items, removing an unknown item returns `false` instead of throwing, and the whole solution builds warning-free with no analyzer exemptions. Correctness is guarded by seeded randomized oracle tests (hundreds of operations compared per-op against a naive scan). Steady-state update cost dropped from 229 µs to ~0.6 µs on the RUM-tree (with ~1000× less allocation); the LGPL obligation, log4net and the last `ConfigurationManager` usage left the repo with the ports (git history preserves them).
- **MapLibre instead of Bing Maps.** The retired Bing Maps WPF control (and with it the repo's last .NET Framework-era package) was replaced by MapLibre GL JS hosted in WebView2: `wwwroot/map.html` renders OpenStreetMap raster tiles (no API key), and the `MapLibreMap` class bridges C# and the map over JSON web messages (markers, lines, polygons in; click/mousemove with geographic coordinates out — which also replaced the old `ViewportPointToLocation` conversion). Because WebView2 is HWND-hosted and always paints above WPF content, the control panel now sits beside the map rather than overlaying it. Note MapLibre zoom levels use the 512px-tile convention — one level lower than the equivalent Bing value.
- **Idiomatic C# 14 review.** A full sweep for the library's Java heritage: `IContainer`/`GeoList`/`Grid`/`Tree` became `ISpatialBackend`/`ListBackend`/`GridBackend`/`TreeBackend`, `dump()` became `ToString()`, `AvgCalc` became `RunningAverage`, and the twelve continuous-query observers were rebuilt on primary-constructor bases (`RxObserverBase`/`RxRxObserverBase`, `VehicleObserverBase<TValue>` in the demo) that supply the `IObserver<T>` boilerplate so each query is a single expression. Public members were renamed to .NET conventions (`loc` → `Location`, `latency` → `AverageLatency`, `checkQueuedPushNum` → `QueuedPushCount`, `RxSpatialOverLoadException` → `RxSpatialOverloadException`), and the buffered drainer now goes to the thread pool instead of constructing an OS thread per push burst (~2× faster on the push-wait-push pattern a moving object produces). Reviewing the discarded `IDisposable` in the backends' `Subscribe` overloads turned up two further defects, both fixed and covered by tests: every spatial backend re-registered a disposed observer when the object it tracked next moved, and the grid could not unsubscribe a region observer (a geofence) at all. The build enforces the standard — `AnalysisMode=Recommended` plus the rules covering defect classes this codebase has actually shipped (culture-dependent formatting, discarded disposables, unqualified P/Invoke search paths), with the handful of deliberate exemptions recorded in `.editorconfig` alongside the reason.
