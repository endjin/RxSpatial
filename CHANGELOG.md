# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project aims to follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html) from its first release.

RxSpatial has not been released as a package. The baseline for the changes below is the original research code from the SIGMOD/DEBS 2016 demos (commit `8f10105`, 28 June 2016): .NET Framework 4.0/4.5, which no longer built from a fresh clone. The modernisation took place from July to October 2026. Before/after measurements for every performance and memory change are in [docs/performance.md](docs/performance.md).

## [Unreleased]

### Added

- **Cross-platform shape library: `RxSpatial.Geo`.**
  - A new, zero-dependency library on `net10.0`, written from scratch.
  - Geodetic shapes on WGS84: `GeoPoint`, `GeoPointShape`, exact `GeoCircle`, convex `GeoPolygon` (with `Rectangle`, `FromWkt` and `FromVertices`) and `GeoEmpty`.
  - `Wgs84`: Vincenty inverse and direct solutions, plus a `FastDistance` guard.
  - `LocalPlane`: an azimuthal-equidistant projection.
  - `GeoOperations`: `Intersects`, `Intersection` (including an exact lens for two circles), `Distance`, `Area`, `Bounds` and `ToVertices`, with overloads that write into a caller-supplied span.
  - A planar twin: `PlanarShape`, `PlanarPolygon`, `PlanarCircle` and `PlanarOperations`.
- **Planar moving object: `RxGeometry`.** The planar counterpart of `RxGeography` described in the ICDE 2017 paper, with an `RxGeometryObservable` extension class. It comes with `PlanarGridOptions` and `PlanarCoverageRegionOptions`, bound from `RxSpatial:PlanarGrid`.
- **Windows-only SQL Server package: `RxSpatial.SqlServer`.**
  - `RxSqlGeography` and `RxSqlGeometry` keep the previous `SqlGeography`/`SqlGeometry` API.
  - It serves as the fidelity reference, and as a migration path for existing users.
- **`IMovingObject<TShape>`,** a public interface implemented by every moving object.
- **Spatial index: `RxSpatial.Index`.**
  - A clean-room, zero-dependency R\*-tree engine shared by `RStarTree` (item-keyed) and `RumTree` (update memo, lazy deletion, cleaning token).
  - Seeded randomized tests compare it with a brute-force reference implementation.
- **Typed configuration: `RxSpatialOptions`.**
  - It can be passed to constructors, or set as an ambient default bound from Microsoft.Extensions.Configuration.
  - The samples use `appsettings.json`.
- **Rx-style query API: `RxGeographyObservable`.** C# 14 extension members exposing the queries as Rx streams: `Locations`, `DistanceTo`, `Intersects`, `IntersectionWith`.
- **Tests.**
  - `tests/RxSpatial.Tests`, cross-platform, using MSTest.Sdk, Shouldly and the Microsoft Testing Platform runner.
  - `tests/RxSpatial.SqlServer.Tests`, Windows-only.
  - SQL Server reference values: 201 cases in `tests/golden/*.json`.
  - GeographicLib as an independent test-only oracle.
  - Randomized property tests, and tests that assert zero allocation on hot paths.
- **Benchmarks.** A BenchmarkDotNet suite, `benchmarks/RxSpatial.Benchmarks`:
  - spatial backends and tree indexes, including churn and local-update cases;
  - Geo operations, geodesic primitives, shape construction, outlines, and multi-thread scaling.
  - The Windows-only `benchmarks/RxSpatial.SqlServer.Benchmarks` compares SQL Server with Geo, operation by operation.
- **ICDE 2017 experiments: `benchmarks/RxSpatial.Experiments`.** A harness reproducing Figs. 6–12 (latency, maximum load, dropped updates). Its results are compared with the paper in the README.
- **Windows power-throttling opt-out** for measurement harnesses. Set `RXSPATIAL_ALLOW_POWER_THROTTLING=1` to keep the operating system default.
- **Console examples: `samples/RxSpatial.Examples`.** A cross-platform scenario runner, which also serves as the end-to-end smoke test.
- **WPF map demo (`samples/MapRunRxSpatial`):**
  - Per-scenario tabs: *Geofence*, *Near area*, *Near vehicle*, *Convoy link* and *Overlap*. Each tab has guidance that changes with it, a colour legend, and default settings taken from the papers' figures.
  - Geofence confinement zones: an alert fires when a vehicle leaves every confinement area (ICDE 2017 Fig. 4a).
  - An on-screen choice of spatial index (List, Grid, R\*-tree, RUM-tree).
  - Overlap tab: a reach circle per vehicle, overlap lenses with their area, and zone overlap. It exercises `RxIntersection`, `RxRxIntersect` and `RxRxIntersection`.
  - *Watch all pairs*: a self-join over every pair of vehicles (ICDE 2017 §V.B).
  - An explicit area-drawing mode: drag to draw, Esc or right-click to cancel.
  - A `MapRunRxSpatial` metrics meter for `dotnet-counters`.
- **Documentation:**
  - `docs/performance.md`: every performance and memory pass, with its measurements.
  - Citations for the SIGSPATIAL 2016 and ICDE 2017 papers.
  - An explanation of the `Rx`/`RxRx` naming.
  - `CLAUDE.md`, the project memory for AI-assisted development.
- **Build and CI:**
  - A CI matrix on Ubuntu, Windows and macOS. Linux and macOS build and test the cross-platform projects.
  - The standard endjin ZeroFailed build (`build.ps1`, `.zf/config.ps1`, `GitVersion.yml`): versioning, build, tests with code coverage, the examples smoke test, a Covenant SBOM and packaging. CI runs it on Windows over `RxSpatial.slnx`, and on Ubuntu and macOS over the new `RxSpatial.CrossPlatform.slnf`.
  - Dependabot for NuGet and GitHub Actions, with the endjin approve-and-label and auto-release workflows.
  - NuGet packages for the three libraries (`RxSpatial`, `RxSpatial.Geo`, `RxSpatial.Index`), attached to a GitHub release by a tag build. They are not pushed to nuget.org.
  - Central package management, a shared `Directory.Build.props`, a pinned `global.json` and an `.editorconfig`.
  - Analyzers at `AnalysisMode=Recommended`, plus CA1305, CA2000, CA5392 and CA1852.
- **Licence:** the MIT License (`LICENSE`) and a contributor list (`AUTHORS.txt`), matching the licence that the original authors added upstream (`williamshyy/RxSpatial` commit `b1caf5b`, 23 October 2026).

### Changed

- **BREAKING: the core is cross-platform.** `RxGeography` now works over `GeoShape`, and `RxGeometry` over `PlanarShape`, instead of `SqlGeography`/`SqlGeometry`. Member names are unchanged. SQL Server users can move to `RxSqlGeography`/`RxSqlGeometry` in `RxSpatial.SqlServer`.
- **BREAKING: distances are `double`.** `RxDistance`, `RxRxDistance` and `DistanceTo` use `double` instead of `SqlDouble`, matching the papers' signatures. A distance that cannot be computed is reported as `double.NaN`, so every location update still produces one result.
- **BREAKING: public renames to .NET naming conventions:**
  - `loc` → `Location`
  - `latency` → `AverageLatency`
  - `checkQueuedPushNum` → `QueuedPushCount`
  - `RxSpatialOverLoadException` → `RxSpatialOverloadException`
  - The containers became `ISpatialBackend<TShape, T>`, with `ListBackend`, `GridBackend` and `TreeBackend`.
  - `Unsubscriber<T>` changed signature to match the generic backend interface.
- **BREAKING: `AverageLatency` reports total milliseconds,** including fractions. It used to report the millisecond component, which read sub-millisecond updates as 0 and wrapped round at one second.
- **BREAKING: removed `HighResolutionDateTime`.** Latency is now timed with `Stopwatch`.
- **Modernised to .NET 10 and C# 14** throughout:
  - SDK-style projects, arranged in a `src`/`samples`/`benchmarks`/`tests`/`docs` layout with `RxSpatial.slnx`.
  - Nullable reference types and file-scoped namespaces.
  - Idiomatic C# 14, with observers built on primary-constructor base classes.
- **Upgraded System.Reactive** from the Rx 2.2.5 `Rx-*` packages to 7.0.0.
- **Upgraded Microsoft.SqlServer.Types to 170.1000.7,** now confined to the Windows-only `RxSpatial.SqlServer` package.
- **One shared engine.** `RxGeography` and `RxGeometry` are thin facades over a single struct-generic engine, `MovingObjectCore<TShape, TOps>`.
- **Backends index every shape by its bounding rectangle.**
- **WPF demo: map and controls.**
  - Bing Maps is replaced by MapLibre GL JS in WebView2, with OpenStreetMap tiles and no API key.
  - The control panel sits beside the map.
  - Tab names and labels are in plain language.
  - The window is 1300×880, limited to the screen's working area.
- **WPF demo: colours and accessibility.**
  - Colour bands are ordered by urgency. Yellow is the warning band, as in the papers. Vehicle pairs closer than 1/7 of the observe distance turn red. Watched pairs that are out of range are gray.
  - Each vehicle state has its own marker shape: circle, dashed ring, rounded square, triangle, diamond.
  - Restricted areas are drawn with slate hatching and confinement areas with a dashed purple outline; zones and lenses have their own styles.
  - The Windows 11 Fluent theme, with text and controls meeting WCAG 2.2 AA contrast. A dark-blue accent replaces a light system accent that would fail contrast.
  - Sliders have accessible names, status changes are announced, and keyboard navigation works.
- **WPF demo: replay rate.** The replay achieves the rate the slider shows: 25 and 50 updates/s used to run at 21 and 32.
- **Performance:**
  - The R\*-tree and RUM-tree were tuned with profiles, and gained an in-place update for local moves. A RUM-tree update went from 229 µs to 609 ns, and a 90-second soak ran 4.14× more operations.
  - Buffered updates are dispatched to the thread pool, about 2× faster.
  - `RxSpatial.Geo` hot paths were optimised with bit-identical results. Circle–polygon intersects is 6× faster, and point-in-polygon 2×.
  - Geo's `Intersection` reuses per-thread scratch buffers, cutting its allocations by 34–58 %.
  - The WPF demo sends one web message per replay step, containing only what changed, and caps map redraws at 60 per second. The heavy tabs now use about half the CPU per step.
- **Memory (WPF demo):**
  - The panel is rendered in software, and V8's young generation is capped. App working set is about 40 % lower (for example 246 → 152 MB), and the renderer about 50 % lower on the heavy tabs (218 → 104 MB).
  - Dynamic PGO is off.
  - When the window is minimised, the map is hidden and WebView2 is asked to use less memory.

### Deprecated

- **`RxSpatial.SqlServer` (`RxSqlGeography`, `RxSqlGeometry`)** is kept as the Windows-only fidelity reference and migration path. New code should use the cross-platform `RxGeography`/`RxGeometry` with `RxSpatial.Geo` shapes. Microsoft.SqlServer.Types 170.x ships under pre-release (CTP) licence terms. The package, its tests and its benchmarks are archived in `solutions/archive/`, with their own `RxSpatial.Archive.slnx`. They are no longer in the main solution, so CI no longer builds, tests or inventories them.

### Removed

- The vendored LGPL Java ports of the R-tree and RUM-tree (4,068 lines), and the log4net dependency.
- `System.Configuration.ConfigurationManager` and the `App.config` settings.
- The Bing Maps WPF control, the last .NET Framework-era package.
- The 2016 `LoadTestApplication` and `memoryTestApplication` consoles, replaced by BenchmarkDotNet and the experiments harness. They remain in the git history.
- The core library's dependency on Windows and Microsoft.SqlServer.Types.

### Fixed

- **Backend subscriptions:**
  - Disposing a subscription on a tree backend never detached the observer. `Remove` and `Contains` looked in the wrong index map.
  - The backends now dispose the location tracker they create, so a later update no longer re-registers an observer that has been removed.
- **Grid backend:**
  - The cell maths was wrong for non-square grids (`longStep` and the cell-key stride).
  - The point-handling branch could never run, because it compared a `SqlString` with a `string`.
  - Region observers are recorded in every cell they cover.
  - A region update now notifies a region observer once, not once per shared cell.
- **WKT culture bug:** in locales with a comma decimal separator, the WPF demo wrote invalid WKT.
- **WPF demo:**
  - The pair-subscription bug, where the status could not distinguish "not started" from "duplicate".
  - A stale connection line when a pair turned red.
  - A connection line that could take another vehicle's line ID.
  - A page event-listener leak across tab switches.
  - The "all pairs" status, which didn't update after Start.
  - The guidance panel overflowing the window.
- **Measurement accuracy:** Windows power throttling had made harness scaling results read about 1×. The harness now opts out, and shows Geo scaling 7.9× on 20 threads.

## Migration notes

### Breaking changes for `RxGeography` and `RxGeometry`

The core no longer depends on Microsoft.SqlServer.Types:

| Before                                                                                                                                                | After                                                                                                   |
|-------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------|
| `RxGeography : IObserver<SqlGeography>, IObservable<SqlGeography>`                                                                                    | `RxGeography : IObserver<GeoShape>, IObservable<GeoShape>`                                              |
| `RxGeometry : IObserver<SqlGeometry>, IObservable<SqlGeometry>`                                                                                       | `RxGeometry : IObserver<PlanarShape>, IObservable<PlanarShape>`                                         |
| `Location` is a `SqlGeography?` / `SqlGeometry?`                                                                                                      | `Location` is a `GeoShape?` / `PlanarShape?`                                                            |
| `RxIntersect`, `RxIntersection`, `RxDistance` and the `RxGeographyObservable` / `RxGeometryObservable` extensions take `SqlGeography` / `SqlGeometry` | They take `GeoShape` / `PlanarShape`; the member names and parameters are unchanged                     |
| `RxIntersection` yields whatever `STIntersection` returns                                                                                             | It yields `GeoEmpty`, a point (touching shapes), the contained shape, or a convex polygon               |
| Distance NaN for different SRIDs or an empty geography                                                                                                | NaN for an empty shape or a non-converging (nearly antipodal) geodesic; there are no SRIDs              |
| An empty shape pushed to the grid or tree backends throws `SqlNullValueException`                                                                     | It throws `InvalidOperationException` (an empty shape has no bounds); the List backend still accepts it |
| `HighResolutionDateTime` (a Windows P/Invoke clock)                                                                                                   | Removed; latency is timed with `Stopwatch`                                                              |

Translating shapes: `SqlGeography.Point(lat, lon, 4326)` becomes `new GeoPointShape(lat, lon)`; `point.STBuffer(r)` becomes `new GeoCircle(new GeoPoint(lat, lon), r)`; `SqlGeography.STPolyFromText(wkt, 4326)` becomes `GeoPolygon.FromWkt(wkt)` (if the polygon is convex); `SqlGeometry.Point(x, y, 0)` becomes `new PlanarPointShape(x, y)`. `STIntersects`, `STDistance`, `STIntersection` and `STArea` become `GeoOperations.Intersects`, `Distance`, `Intersection` and `Area`.

## Modernisation notes (2026)

The codebase was originally built in 2014-2016 against .NET Framework 4.0/4.5 and no longer compiled from a fresh clone (machine-specific assembly hint paths, missing binaries, TFS bindings). It has been modernised in two passes — first a faithful migration, then a restructure to current standards:

- **.NET 10, C# 14 and SDK-style projects** throughout, arranged one folder per project under `solutions/` (with `docs` at the root) and a single `RxSpatial.slnx` that groups them into `src` / `samples` / `benchmarks` / `tests` solution folders, shared `Directory.Build.props` (nullable, implicit usings, artifacts output, warnings-as-errors in Release), central package management via `Directory.Packages.props`, `.editorconfig`, pinned `global.json`, and the standard endjin ZeroFailed build (`build.ps1`) with Dependabot automation on GitHub Actions (see [Build and CI](docs/build-and-ci.md)).
- **System.Reactive 7.0.0.** Rx upgraded from the 2.2.5 `Rx-*` package family; the `RxGeographyObservable` C# 14 extension members make the library's streams first-class Rx sequences (see [Using RxSpatial](docs/usage.md)).
- **Microsoft.SqlServer.Types 170.1000.7.** The SQL Server 2025 generation of the official package supports modern .NET (netstandard2.0 + native win-x64/x86/arm64 runtimes), so the `SqlGeography` public API of the library was preserved at first (distance results aside — see below). It is now confined to the Windows-only `RxSpatial.SqlServer` package (see the cross-platform note below).
- **Tests and benchmarks.** The original `LoadTestApplication` and `memoryTestApplication` consoles (and the 2016 experiment outputs they had written into their source folders) were replaced by the `RxSpatial.Benchmarks` BenchmarkDotNet suite; behavioural coverage lives in `solutions/RxSpatial.Tests` (MSTest.Sdk). The originals remain in the git history.
- **Modern configuration.** The legacy `System.Configuration.ConfigurationManager` appSettings were replaced by typed `RxSpatialOptions` with constructor injection plus an ambient default bound from Microsoft.Extensions.Configuration (see [Configuration](docs/configuration.md)); the samples now use `appsettings.json` instead of `App.config`. The core library itself has no configuration package dependency at all.
- **Core library refactor.** File-scoped namespaces, nullable annotations that preserve the original null-flow, dead code removed, and a handful of verified bug fixes: the grid backend's non-square-grid maths (`longStep` and the cell-key stride), the grid's dead point-handling branch (`SqlString`-vs-`string` comparison that could never be true), and tree-backend unsubscription (`Remove`/`Contains` previously consulted the wrong index map, so disposing a subscription never detached observers from an R-tree backend).
- **Modern spatial indexes.** The 20-year-old vendored Java ports of the R-tree and RUM-tree (4,068 lines, LGPL 2.1, log4net-era) were replaced by `solutions/RxSpatial.Index`: a clean-room R*-tree engine shared by both trees, with the RUM-tree implemented per the paper (long stamps, per-item update memo, tombstones, a round-robin cleaning token over a leaf ring). The item-keyed API (`Insert`/`Update`/`Remove`/`Search`) made the core library's per-tree rectangle caches unnecessary, `Count` now reports live items, removing an unknown item returns `false` instead of throwing, and the whole solution builds warning-free with no analyzer exemptions. Correctness is guarded by seeded randomized oracle tests (hundreds of operations compared per-op against a naive scan). Steady-state update cost dropped from 229 µs to ~0.6 µs on the RUM-tree (with ~1000× less allocation); the LGPL obligation, log4net and the last `ConfigurationManager` usage left the repo with the ports (git history preserves them).
- **MapLibre instead of Bing Maps.** The retired Bing Maps WPF control (and with it the repo's last .NET Framework-era package) was replaced by MapLibre GL JS hosted in WebView2: `wwwroot/map.html` renders OpenStreetMap raster tiles (no API key), and the `MapLibreMap` class bridges C# and the map over JSON web messages (markers, lines, areas and drawing mode in; drawn areas with geographic coordinates out — which also replaced the old `ViewportPointToLocation` conversion). Because WebView2 is HWND-hosted and always paints above WPF content, the control panel now sits beside the map rather than overlaying it. Note MapLibre zoom levels use the 512px-tile convention — one level lower than the equivalent Bing value.
- **Idiomatic C# 14 review.** A full sweep for the library's Java heritage: `IContainer`/`GeoList`/`Grid`/`Tree` became `ISpatialBackend`/`ListBackend`/`GridBackend`/`TreeBackend`, `dump()` became `ToString()`, `AvgCalc` became `RunningAverage`, and the twelve continuous-query observers were rebuilt on primary-constructor bases (`RxObserverBase`/`RxRxObserverBase`, `VehicleObserverBase<TValue>` in the demo) that supply the `IObserver<T>` boilerplate so each query is a single expression. Public members were renamed to .NET conventions (`loc` → `Location`, `latency` → `AverageLatency`, `checkQueuedPushNum` → `QueuedPushCount`, `RxSpatialOverLoadException` → `RxSpatialOverloadException`), and the buffered drainer now goes to the thread pool instead of constructing an OS thread per push burst (~2× faster on the push-wait-push pattern a moving object produces). Reviewing the discarded `IDisposable` in the backends' `Subscribe` overloads turned up two further defects, both fixed and covered by tests: every spatial backend re-registered a disposed observer when the object it tracked next moved, and the grid could not unsubscribe a region observer (a geofence) at all. The build enforces the standard — `AnalysisMode=Recommended` plus the rules covering defect classes this codebase has actually shipped (culture-dependent formatting, discarded disposables, unqualified P/Invoke search paths), with the handful of deliberate exemptions recorded in `.editorconfig` alongside the reason.
- **Distances as `double` (breaking change).** The 2016 code returned distances as `SqlDouble`, so every consumer had to cast or call `.Value`. The distance queries now take `IObserver<double>` and the `DistanceTo` extensions return `IObservable<double>`, matching the `RxDistance(SqlGeography, IObserver<double>)` / `RxRxDistance(RxGeography, IObserver<double>)` signatures in the ICDE 2017, DEBS 2016 and SIGMOD 2016 papers. Where `STDistance` returns `SqlDouble.Null` (different SRIDs, an empty geography) the query emits `double.NaN`, which keeps one notification per location update; the map demo shows NaN as out of range.
- **`RxGeometry`, the planar moving object.** The ICDE 2017 paper names `RxGeometry` beside `RxGeography` but the authors never released it. The push queue, drainer and backend factory moved out of `RxGeography` into one internal engine, `MovingObjectCore<TShape, TOps>`, with the shape-specific operations in stateless structs whose static members the JIT calls directly; `RxGeography` (public members unchanged) and the new `RxGeometry` are thin facades over it. The grid and tree backends now index every shape by the same bounding rectangle, which made the grid cheaper for region pushes and fixed a grid defect where a region push notified a region observer once per shared cell. The public `ISpatialBackend` and `Unsubscriber` types gained a shape type parameter (`ISpatialBackend<TShape, T>`, `Unsubscriber<TShape>`).
- **Cross-platform core (breaking change).** The core no longer needs Windows or Microsoft.SqlServer.Types. The new `solutions/RxSpatial.Geo` is a clean-room shape library — Vincenty geodesics on WGS84, exact circles with an analytic lens, convex polygons, and planar twins — and `RxGeography` / `RxGeometry` now take `GeoShape` / `PlanarShape` (see [Breaking changes](#breaking-changes-for-rxgeography-and-rxgeometry)). Before the switch, SQL Server's own results for 201 Seattle-scale cases were recorded to `solutions/RxSpatial.Tests/Golden`, and the new maths is tested against them on every platform, against GeographicLib as an independent oracle, and with randomized property tests. The previous facades live on as `RxSqlGeography` / `RxSqlGeometry` in the Windows-only `RxSpatial.SqlServer` package. The latency clock moved from a Windows P/Invoke to `Stopwatch`, the samples and the experiments harness moved to the Geo types (the WPF demo keeps every tab's behaviour), and CI now builds and tests on Ubuntu, Windows and macOS.
- **Map demo performance pass.** Profiling the WPF demo (a UI-automation harness with `dotnet-counters`, `dotnet-trace`, `dotnet-gcdump` and the page's DevTools metrics) showed that the time went to the WebView2 renderer and GPU processes, not to the continuous queries. Three changes followed. `MapLibreMap` now posts one web message per replay tick instead of one per command (25–70), merges updates to the same marker, line or overlay, sends only the marker fields that changed, and writes outlines as integer micro-degree steps straight to UTF-8: on *Overlap* with all pairs watched and a zone, 87 KB in 72 messages became 35 KB in one, and managed allocation fell from about 490 to 160 KB per tick. `map.html` caps MapLibre's data-driven repaints at 60 a second (it had redrawn the map about 220 times a second on a 240 Hz display), which cut the renderer process from 104% to 42% of a core and the GPU process from 131% to 37% on that tab. The replay keeps its own clock, so 25 and 50 updates/s are now achieved (21 and 32 before). The demo publishes a `MapRunRxSpatial` meter (tick and send time, pushes, web messages and characters) for `dotnet-counters`.
- **Map demo memory pass.** A memory breakdown (VMMap, `dotnet-dump` `eeheap`, `dotnet-gcdump`, GPU process memory counters and the page's DevTools metrics) showed that the managed heap was never the problem: about 7 MB live, of which the 20 trajectories are 0.6 MB. The memory was native. WPF rendered its window, a control panel next to a WebView2 child window, through a Direct3D 9 device and a swap chain the size of the screen. The panel is now rendered in software, which takes the app's private bytes from 171–180 MB to about 85 MB on *Overlap* and from 135 to 79 MB at idle. In the map page, V8 had grown its young generation to hold the GeoJSON garbage of every frame. `--js-flags=--max-semi-space-size=2` halves the renderer process on *Overlap* and *Convoy link* (about 200 MB to 105 MB) at the same renderer CPU. Dynamic PGO is off (about 4 MB of code and loader heap). While the window is minimised, the map is hidden and WebView2's memory target is low: the WebView2 processes' working sets fall from about 610 to 100 MB, and the page stops drawing a map that nobody can see. `GeoOperations.Intersection` now builds its rings in per-thread scratch lists, with bit-identical results, which cut managed allocation on *Overlap* by 28%.


[Unreleased]: https://github.com/endjin/RxSpatial/compare/8f10105...HEAD
