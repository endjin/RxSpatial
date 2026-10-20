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
  - Central package management, a shared `Directory.Build.props`, a pinned `global.json` and an `.editorconfig`.
  - Analyzers at `AnalysisMode=Recommended`, plus CA1305, CA2000, CA5392 and CA1852.

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

- **`RxSpatial.SqlServer` (`RxSqlGeography`, `RxSqlGeometry`)** is kept as the Windows-only fidelity reference and migration path. New code should use the cross-platform `RxGeography`/`RxGeometry` with `RxSpatial.Geo` shapes. Microsoft.SqlServer.Types 170.x ships under pre-release (CTP) licence terms.

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

[Unreleased]: https://github.com/endjin/RxSpatial/compare/8f10105...HEAD
