# CLAUDE.md

RxSpatial is the SIGMOD/DEBS 2016 research library for reactive spatio-temporal queries
(`RxGeography` geodetic and `RxGeometry` planar moving objects + continuous
distance/intersect/intersection queries), fully
modernised to .NET 10 / C# 14 / System.Reactive 7 in July 2026, and cross-platform since
October 2026: the core runs on Windows, Linux and macOS over the clean-room `RxSpatial.Geo`
shape maths; only `RxSpatial.SqlServer` (the SQL Server facades) and the WPF demo are
Windows-only. House standards live in `.endjin/llm-kb/dotnet` (gitignored) — consult
before .NET work.

## Commands

```shell
dotnet build RxSpatial.slnx -c Release   # TreatWarningsAsErrors in Release; must stay warning-free (Windows: the slnx has Windows-only projects)
dotnet test -c Release                   # 377 MSTest tests, ~3s: RxSpatial.Tests 285 + RxSpatial.SqlServer.Tests 92 (1 golden recorder skipped); MTP runner via global.json "test" key
dotnet run --project samples/RxSpatial.Examples -c Release   # end-to-end smoke: "All scenarios passed." (any OS)
dotnet run --project benchmarks/RxSpatial.Benchmarks -c Release -- --filter "*TreeIndexBenchmarks*"
dotnet run --project benchmarks/RxSpatial.Benchmarks -c Release -- --filter "*Geo*" "*Outline*" "*ShapeConstruction*"   # RxSpatial.Geo: ops, primitives, construction, outlines, 1–20-thread scaling (~20 min)
dotnet run --project benchmarks/RxSpatial.SqlServer.Benchmarks -c Release -- --filter "*SqlVsGeo*" --job short   # SQL baseline vs Geo, per operation
dotnet run --project benchmarks/RxSpatial.Experiments -c Release -- all   # ICDE 2017 Figs 6–12, ~45 min wall-clock

# Linux (the committed HEAD; the slnx does not build there): Docker Desktop, Linux engine, from bash
git archive HEAD | docker run --rm -i mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  'mkdir /w && tar -xf - -C /w && cd /w && dotnet test --project tests/RxSpatial.Tests/RxSpatial.Tests.csproj -c Release && dotnet run --project samples/RxSpatial.Examples -c Release'
```

Layout: `src/RxSpatial` (core), `src/RxSpatial.Geo` (geodetic + planar shapes and maths, zero
deps), `src/RxSpatial.Index` (R*-tree + RUM-tree, zero deps), `src/RxSpatial.SqlServer`
(Windows-only `RxSqlGeography`/`RxSqlGeometry`), `samples/` (Examples console — any OS;
MapRunRxSpatial WPF demo), `benchmarks/` (RxSpatial.Benchmarks and Experiments — any OS;
RxSpatial.SqlServer.Benchmarks — Windows), `tests/` (RxSpatial.Tests — any OS;
RxSpatial.SqlServer.Tests — Windows; `tests/golden/*.json` recorded SQL Server values),
`docs/prototypes` (never-compiled 2016 archives — do not modernise). Central package
management, artifacts output layout, global.json pins SDK + MSTest.Sdk. CI
(`.github/workflows/build.yml`) is a matrix: Windows builds the slnx and runs every test
project; Linux/macOS build the cross-platform projects by path and run RxSpatial.Tests.

## Architecture essentials

- `RxGeography` (`GeoShape`) and `RxGeometry` (`PlanarShape`) are thin public facades over
  one internal engine, `MovingObjectCore<TShape, TOps>` (push queue, drainer, latency, backend
  factory); both implement `IMovingObject<TShape>`. Shape knowledge lives only in
  `ISpatialOperations<TShape>` struct strategies (`GeographyOperations`, `GeometryOperations`
  → `GeoOperations`/`PlanarOperations`) with static abstract members, so `TOps.Distance(a, b)`
  is a JIT-specialised direct call.
- `RxSpatial.Geo` (public, zero deps): `GeoPoint` (lat, lon; validates), closed `GeoShape`
  hierarchy `GeoPointShape`/`GeoCircle` (exact, radius ≤ 250 km)/`GeoPolygon` (convex only,
  stored CCW; `Rectangle`, `FromVertices`, `FromWkt` lon-first)/`GeoEmpty`, `Wgs84` (Vincenty
  inverse/direct, ≤200 iterations, NaN on non-convergence; `FastDistance` guard), `LocalPlane`
  (azimuthal equidistant), `GeoOperations` (Intersects/Intersection/Distance/Area/Bounds/
  ToVertices, 128 segments). Planar twin `PlanarShape`/`PlanarOperations`, exact. Shapes > 500 km
  (`MaxExtent`) are rejected; `Bounds` is degree-based and does not split at ±180°. Circle∩circle
  is the analytic lens as a `GeoPolygon` with arc vertices (its `Area` is the exact curved one);
  polygon clipping runs in the local plane. Hot paths allocate 0 B; Bounds (and a polygon's Area)
  are computed at construction, so those benchmarks read a field. Its `InternalsVisibleTo`
  names the `RxSpatial.Tests` assembly — keep that name.
- `RxSpatial.SqlServer` (net10.0, `[SupportedOSPlatform("windows")]`): `RxSqlGeography`/
  `RxSqlGeometry` + their observable extensions — the pre-Geo facades over the same engine, with
  `SqlGeographyOperations`/`SqlGeometryOperations`, `SpatialBounds`, `SpatialDistance`. Reaches the
  engine through `InternalsVisibleTo("RxSpatial.SqlServer")` in `src/RxSpatial/RxSpatial.csproj`.
  Only it, its tests and its benchmarks use Microsoft.SqlServer.Types (170.x: pre-release/CTP
  licence terms — README says so).
- Observers are held in an `ISpatialBackend<TShape, T>` (`Backends/`): `ListBackend`,
  `GridBackend` (spatial hash), or `TreeBackend` over `RxSpatial.Index`. Backends are
  shape-agnostic: they index `TOps.BoundsOf(shape)` (a `Rect`), and the grid lays its cells
  out from a `GridLayout`. Backend chosen by `RxSpatialOptions.PreferredDataStruct`.
- `RxSpatial.Index`: one shared engine (`RStarCore`, struct-generic leaf filters) under two
  facades — `RStarTree` (item-keyed, physical updates) and `RumTree` (update-memo, lazy
  deletion, cleaning token over a circular leaf ring). Correctness anchor:
  `SpatialIndexOracleTests` (seeded randomized workloads vs naive-scan oracle) — keep green
  through any engine change.
- `RxGeographyObservable` / `RxGeometryObservable` expose the query API as Rx.NET streams via
  C# 14 extension members (`extension(RxGeography source)`).
- Fidelity vs SQL Server is pinned by `tests/golden/{geodetic,planar}.json` (170 + 31 cases,
  recorded by `GoldenValueRecorder` in RxSpatial.SqlServer.Tests under
  `RXSPATIAL_RECORD_GOLDEN=1`): `GoldenFidelityTests` (any OS) checks Geo within budget
  (point–point max(0.01 %, 1 cm); point–circle/polygon 0.5 m, + 128-gon chord sag for circles;
  areas 0.15 %; intersects agree > 0.5 m from a boundary); `GoldenValueGuardTests` (Windows)
  checks live SQL Server still matches the JSON. GeographicLib.NET is a test-only oracle.

## Modernisation history (July–October 2026, per-phase commits on master)

1. .NET 10 + System.Reactive 7 + Microsoft.SqlServer.Types 170.x migration; load/memory test
   apps replaced with BenchmarkDotNet suite.
2. Repo restructure to endjin standards (src/samples/benchmarks/tests/docs, CPM,
   Directory.Build.props, CI workflow, README rewrite).
3. Typed configuration (`RxSpatialOptions` + ambient `Default`); samples use appsettings.json.
4. WPF demo: Bing Maps → MapLibre GL JS in WebView2 (`wwwroot/map.html` + `MapLibreMap`
   facade over JSON web messages; OSM tiles, no API key).
5. Vendored LGPL Java-port trees deleted; clean-room `RxSpatial.Index` written (commits
   9c5deab..9c64402). Removed LGPL, log4net, ConfigurationManager.
6. Coverage/profiling cycles (4e38362, c7f1a31, 3f29809): dotnet-coverage drove tests
   60.9%→82.9% (Index 96%), dotnet-trace/dotnet-dump drove a ChooseSubtree containment fast
   path + ArrayPool reinsertion scratch (RumTree update 2.78µs→0.6µs; 4.14× soak throughput),
   then Churn/UpdateLocal benchmarks drove a bottom-up in-place update (local moves 0.42× of
   teleports, ~0 alloc). R* wins tracking workloads; RUM wins teleport-heavy updates.
7. Full idiom review (4b50358, 9b55f1c): Java-isms purged from core (ISpatialBackend,
   observer family on primary-ctor bases, public renames `loc`→`Location`,
   `latency`→`AverageLatency`, `RxSpatialOverloadException` casing) and the WPF sample
   (MovingObjectManager/PolygonManager/VehicleMarker/DemoParameters, nullable enabled,
   handler consolidation, WKT culture bug fixed with FormattableString.Invariant).
8. Second idiom pass over everything phase 7 missed — Index, tests, benchmarks, Examples,
   build config — driven by an `AnalysisMode=All` diagnostic run (8612170, 760dfa4, 871ca57,
   2ad73cb). Two real defects found via the discarded `IDisposable` in the backends'
   `Subscribe` overloads (see Gotchas), observer bases now carry the `IObserver` boilerplate,
   `RumTreeOptions.Validate` stopped hiding its base with `new`, drainer moved to the thread
   pool (~2×), stringly-typed `[DataRow]`s became enums, and the standard is now enforced
   in-build (`AnalysisMode=Recommended` + CA1305/CA2000/CA5392/CA1852; exemptions justified
   in `.editorconfig`).
9. WPF demo rework (Oct 2026; ef4f87f, 52921bf, a8efcb1): urgency-ordered colour bands with a
   minimum safe distance, one connection line per pair, and a per-scenario control panel
   (Geofence / Near area / Near vehicle / Convoy link) with legends and a backend footer.
   Follow-ups: controls default to the ICDE 2017 / DEBS 2016 figure settings, including
   pre-watched pairs (aa8815d); window sized to the work area (bf9fe2d); darker out-of-range
   line (c838cf6); areas drawn by dragging in an explicit drawing mode, styled to keep the map
   readable (slate hatch / purple dashed outline); a live Spatial index selector on Geofence
   (appsettings.json sets only its start/reset value); a fifth tab, Overlap, for the three
   intersection queries (RxRxIntersect, RxRxIntersection, RxIntersection) on buffered reaches,
   after DEBS 2016 §3.1/§3.3. The papers (ICDE full paper, DEBS and
   SIGMOD demos) are in the authors' ZIP:
   homepage.cs.uri.edu/~ahendawi/materials/RxSpatilPublications.zip.
10. `RxGeometry`, the planar moving object the ICDE 2017 paper names but never released
    (Oct 2026; 8dae8d1, 262b947, bb620ae, plus docs/benchmark commits). The grid indexes by
    the shared `Rect` (allocation −5 KB/push on HashMap); the engine moved out of `RxGeography`
    into `MovingObjectCore<TShape, TOps>` with `ISpatialOperations<TShape>` struct strategies
    (B/op identical on every `SpatialBackendBenchmarks` row); `RxGeometry` +
    `RxGeometryObservable` + `PlanarGridOptions` added with 44 tests, which also exposed and
    fixed per-shared-cell duplicate grid notifications. Public deltas: `ISpatialBackend<TShape,
    T>`, `Unsubscriber<TShape>`, `IMovingObject<TShape>`. `PlanarBackendBenchmarks` mirrors
    the geographic benchmark.
11. ICDE 2017 §VI reproduction (Oct 2026): `benchmarks/RxSpatial.Experiments`, a paced
    wall-clock harness (`service`, `fig6`…`fig12`, `all`, `--full`) writing CSV + summary.md to
    `artifacts/experiments/`. SqlSpatial = List backend (the paper's own wording; a plain
    STIntersects loop is within 8%). Reproduced: variant ordering, latency/max-load trends,
    SqlSpatial/Grid drops (RUM 175× SqlSpatial at 200 ms vs the paper's 55×). Not resolvable:
    R-tree vs RUM (both sub-ms, dispatch noise; both ≥ object cap). Not reproduced: Fig. 12
    drops (load too light now). `AverageLatency` switched to total fractional ms.
12. Cross-platform core (Oct 2026; plan and evidence in the session's research report). Agent A
    (ace954a..5c1b82f): SQL Server golden values recorded first, `LocationPush` on Stopwatch
    (HighResolutionDateTime P/Invoke removed), clean-room `src/RxSpatial.Geo` + 131 tests (unit,
    property, GeographicLib oracle, golden fidelity, allocation), Geo benchmarks. Agent B
    (239047d, 16f4e07): `src/RxSpatial.SqlServer` (`RxSqlGeography`/`RxSqlGeometry`), core
    switched to `GeoShape`/`PlanarShape` (breaking), tests split into RxSpatial.Tests (any OS) +
    RxSpatial.SqlServer.Tests (Windows; recorder + live guard), benchmarks split. Agent C
    (dcb20cb..): Examples and Experiments on Geo (net10.0), WPF demo on Geo (no SqlServer.Types;
    reach outline from `ToVertices`), `SqlVsGeoOperationsBenchmarks`, CI matrix (ubuntu/windows/
    macos), README/CLAUDE.md. Measured: Geo point-in-circle 20 ns vs SQL 58 µs, disc–disc 23 ns vs
    226 µs, lens 21 µs vs 190 µs; point–point distance is the one op SQL wins (86 vs 152 ns). The
    experiments' List ("SqlSpatial") update fell 109 ms → 67 µs, so the paper's orders-of-magnitude
    gaps between List and the trees collapse to sub-ms dispatch noise on Geo.
13. Geo performance pass (Oct 2026; 44cea45..0038414). Scaling mystery solved: Windows 11
    power-throttles (EcoQoS) background-console processes, ~6.7× slower per core ~4 s into load
    on this hybrid laptop; the `service` table measured 1 thread fresh and the rest throttled
    (~1×). Harness now opts out (`benchmarks/Shared/PowerThrottling.cs`, env
    `RXSPATIAL_ALLOW_POWER_THROTTLING=1` to compare) and reports busy cores + a repeated
    1-thread row: 7.88×. New benchmarks: Geodesy, ShapeConstruction, Outline, GeoScaling
    (dedicated threads, padded counter/slots, Mops/s + Scaling columns). Optimisations, all
    bit-identical (100k-pair differential vs the pre-optimisation build): IEEERemainder skipped
    for |Δλ| ≤ 180; bounds by `ref readonly` (AVX–SSE penalty, see Gotchas); `ReducedPoint`
    hoisting; polygon distance shortlists edges in the polygon's plane within a proven slack;
    circle–polygon Intersects decided in that plane (1,036 → 173 ns); point-in-polygon guarded
    by `Wgs84.FastOffset` with a 2×-measured bound (54 → 28 ns); `ToVertices(shape, Span)`
    overloads (0 B). Vincenty itself and backend push rows unchanged.
14. WPF demo performance pass (Oct 2026; e1895a4..b055ed9). Profiled with a UI-automation
    harness (dotnet-counters on a new `MapRunRxSpatial` meter, dotnet-trace, dotnet-gcdump,
    DevTools metrics/profiles over CDP, cycle-counted CPU). The queries were never the cost
    (UI thread ≤15%); the WebView2 renderer and GPU processes were, at >1 core each on
    Overlap/Convoy link. One web message per tick with merged, changed-only commands written
    by Utf8JsonWriter (Overlap: 72 msgs / 87 KB → 1 / 35 KB per tick, allocation 490 → 160
    KB/tick, UI 5.5 → 2.6 ms/tick); MapLibre data-driven repaints capped at 60/s (renderer
    104 → 42%, GPU 131 → 37% on Overlap at the same rate); replay on its own clock (25/50
    updates/s achieved instead of 21/32). 5-minute tab-switch soak: no managed or DOM growth.

## Performance: before / after (N=1000, Release, BenchmarkDotNet + 90s soak)

| Metric                            | Vendored Java port (start) | Clean-room rewrite    | + profile-guided opt (4e38362) | + in-place update (3f29809)          |
|-----------------------------------|----------------------------|-----------------------|--------------------------------|--------------------------------------|
| RUM-tree update                   | 229,330 ns / 18,133 B      | 2,784 ns / 27 B (82×) | **609 ns / 17 B (377×)**       | unchanged (RUM never removes)        |
| R\*-tree update (teleport)        | 602 ns / 447 B ¹           | 1,237 ns / 19 B       | **1,009 ns / 11 B**            | unchanged (fallback path)            |
| R\*-tree update (local move)      | —                          | 0.90× of teleport ²   | —                              | **0.42× of teleport, 1 B** ²         |
| Intersection query                | ~64 ns ³                   | ~70 ns / 46 B         | unchanged                      | unchanged; buffered overload **0 B** |
| Bulk insert (R\* / RUM)           | —                          | 2.31 / 4.37 ms        | **1.92 / 2.60 ms**             | unchanged                            |
| 90s mixed soak (both trees)       | —                          | 17.34M ops            | **71.86M ops (4.14×)** ⁴       | —                                    |
| Test line coverage (core / Index) | —                          | 60.9% / 96.0%         | 82.9% / 96.0%                  | (coverage cycle, c7f1a31)            |

¹ The vendored update baseline is a Guttman R-tree (cheaper ChooseSubtree, worse query
quality); the rewrite pays the R\* overlap criterion for better trees. ² Same-run ratio —
this workstation's absolutes swing ~2× with load, so cross-run absolutes are not comparable;
the UpdateLocal:Update ratio is the honest metric (946 vs 2,256 ns in one run). ³ Vendored
query numbers were near-parity; queries were never the bottleneck. ⁴ Identical
checksum-per-op to six digits, independently confirming unchanged search semantics.

Headline: the optimisations reversed the initial honest finding — memo-based lazy deletion
(RUM) *now beats* direct R\* remove+insert in memory (0.60×), vindicating the paper's thesis,
and the R\*-tree with the in-place fast path wins tracking workloads outright.

Off-table (871ca57, different axis — the buffered push path, not the index): dispatching the
drainer to the thread pool instead of `new Thread(...)` per burst took a push-wait-push cycle
from 438/442/411 µs to 182/165/247 µs (three runs each, same session, sleep-free spin so
`Thread.Sleep` granularity cannot swamp it). ~2×, with a wider spread — a fixed construction
cost traded for scheduling that depends on pool state.

## Gotchas and conventions

- **Never push** without being asked; commit per logical phase on master.
- `RxSpatialOptions.Default` is process-global → tests run sequentially by design.
- Tree/grid backends: `RxGeography.Location` is null until the first `OnNext` push, so
  RxRx subscriptions before a first push fail (preserved original behaviour). Tests that
  exercise tree-backend zone notification push small discs (`GeoCircle`/`PlanarCircle`; on
  the RxSql* facades `.STBuffer(...)`), not bare points.
- RUM-tree stability law: steady pending garbage ≈ CleaningInterval × leaf count; keep the
  interval well below average leaf fill.
- Benchmark absolutes on this workstation swing ~2× with load — compare within-run ratios
  (or a 90s soak) across sessions, never cross-run absolutes. Under `dotnet-coverage`, run
  benchmarks `--inProcess --invocationCount 512 --unrollFactor 1` (a Dry job undercounts
  loop paths); a hung `dotnet test` under the collector is usually stale MSBuild daemon
  nodes → `dotnet build-server shutdown`.
- BenchmarkDotNet rebuilds from source per benchmark: don't edit `src/` while a run is live.
- Windows FS is case-insensitive: case-only file renames need `git mv`.
- WPF: shared slider handlers fire during InitializeComponent (Minimum coerces Value) before
  later tabs' named elements exist — cross-tab control access must be null-tolerant.
- WPF Geofence Spatial index: the backend is baked into each `RxGeography`, so a switch sets
  `RxSpatialOptions.Default.PreferredDataStruct` and calls `ClearAllVehicles()`; drawn areas
  stay and `AddVehicle` re-subscribes each recreated vehicle (after its first push). The
  handler acts only when `ActiveScenario` is Intersect, so `ApplyControlDefaults` checks the
  reset button *after* assigning `ActiveScenario`; `ResetScenario` sets the backend itself.
- WPF demo theme: Fluent light via `ThemeMode="Light"` in App.xaml (Mica on Windows 11). The
  API is `[Experimental("WPF0001")]` in code, but the XAML attribute raises no diagnostic, so
  no suppression is needed — keep it in XAML. Fluent styles are app-level *implicit* styles:
  a keyed `Style` must be `BasedOn="{StaticResource {x:Type Button}}"` (or `AccentButtonStyle`)
  or the control silently falls back to the classic grey look. The custom `TabControl`
  template must name its presenter `PART_SelectedContentHost`, or UI Automation cannot see
  any tab content (every button "not found").
- WPF demo contrast floor is WCAG 2.2 AA (4.5:1 text, 3:1 control states). Never use
  `TextFillColorTertiaryBrush` for meaningful text (3.3:1). WPF renders *translucent* text
  far lighter than its composited colour (Fluent secondary #9E000000: 6.1:1 on paper, 3.2:1
  in pixels), so `AccessibleTheme` (App.OnStartup) swaps the secondary brush for opaque
  #5C5C5C, and pins the accent brushes to #005A9E when white text on the user's accent (80%
  pressed state) would fail; both are skipped under high contrast. Measure from PrintWindow
  pixels (darkest glyph pixel vs background), not just composited values. Every marker state
  must have a unique shape — colour is never the only cue (WCAG 1.4.1): blue circle, gray
  dashed ring, green rounded square, yellow triangle, red diamond, kept in step across
  `VehicleColors`, map.html (`MARKER_SHAPES` + CSS) and the legends on all five tabs. Under
  Machado 2009 CVD simulation green/red (deutan 9.1, protan 10.7) and blue/green (tritan 11.5)
  are colour-confusable (ΔE00), gray/green and gray/red weak (<20), so a new state needs a new
  shape, not just a new hue. The triangle
  draws its border as ::before and its fill as ::after (clip-path drops borders and
  box-shadows; the element carries a drop-shadow filter instead).
  Keep the tab header panel `TabNavigation="Once"`: focusing a TabItem selects it, which
  resets the scenario. Five headers fit the 376px panel only at 13px with 6px side padding;
  a sixth tab needs a different strip, not tighter margins.
- MapLibre zoom uses the 512px-tile convention (one level lower than Bing/Leaflet).
- WPF demo colours: `RxGeography` notifies location observers before zone observers, so
  `VehicleMarker.MoveTo` resets the marker to Blue and each query observer may only *raise*
  it (`VehicleColor` is ordered by urgency; most urgent wins whatever the notification order).
  Bands come from `DistanceBands`; pairs go red below 1/7 of the observe distance.
- WPF demo pairs: an RxRx observer fires only when its *own* vehicle moves, so both
  directions of a pair are subscribed; the connection line is keyed by `VehiclePair` and
  upserted by `ConnectionLines` (one MapLibre line per pair, not per observer).
- WPF demo area drawing: the drag and its preview live in `map.html` (drawing mode suspends
  dragPan/dragRotate/boxZoom/doubleClickZoom); C# only sends `beginDraw`/`cancelDraw` and
  receives `areaDrawn`/`drawCancelled`. Esc arrives in two focus domains — the WebView2 HWND
  (JS keydown → `drawCancelled`) or the WPF panel (Window `KeyDown`) — so both paths cancel and
  both are idempotent. Area buttons are `ToggleButton`s driven by Checked/Unchecked (UIA
  `Toggle` raises no Click); `EndAreaDrawing` clears `drawingButton` *before* un-checking so
  its own Unchecked is not taken for a user cancel. Area/preview styles are defined once, in
  `AREA_STYLES` in map.html. Measure pointer positions against `map.getContainer()`: the
  MapLibre canvas container has zero height. UI-automation drags via posted
  `WM_MOUSEMOVE`/`WM_LBUTTONDOWN` to `Chrome_RenderWidgetHostHWND` work (no real cursor).
- WPF demo confinement zones (Geofence): the breach ("outside every confinement area") is
  decided at the end of `MovingObject.PushLocation`, not by an inverted intersect observer —
  tree/grid backends prune notifications to observers whose zone bounds meet the pushed
  location, so a far-outside vehicle never hears from the zone. `ConfinementObserver`s set a
  per-push flag; this relies on visualised mode notifying synchronously inside `OnNext`.
- WPF demo Overlap tab: only there does `MovingObject.PushLocation` push
  `new GeoCircle(position, Reach)` instead of the `GeoPointShape`, and the marker goes to the
  `VehicleFix` kept alongside the push (`MarkerMovementObserver` ignores the pushed value). The
  fix carries that same `GeoCircle`; `MapLibreMap.SetMarker` sends its outline
  (`OutlineWriter`, `ToVertices` span overload, 64 segments to keep 50 Hz × 20 markers' messages small) and
  map.html draws it — map.html no longer computes circles. `VehiclePairConnectionObserver`
  draws only when `other.Location is GeoPointShape`, true because Convoy link pushes points.
  Lenses (`RxRxIntersection`) and zone overlaps (`RxIntersection`) are upserted per
  `VehiclePair` / per vehicle-and-zone by `OverlapShapes`: a `GeoPolygon` or a contained
  `GeoCircle` is drawn from its vertices (one convex ring, never holes or parts), the lens label
  is `GeoOperations.Area` (exact curved area), and `GeoEmpty` or a touching point removes it —
  which is why the tab uses the List backend (a tree/grid prunes the "no longer overlapping"
  notification, so lenses would linger and markers fall back to Blue instead of Gray). Watch
  all pairs there is RxRxIntersect only (with SQL Server's 128-gons, 380 intersects per tick
  took ~40 ms; Geo's exact disc–disc test is ~23 ns, so the limit is now the drawing). map.html
  batches the reach/lens/zone-overlap GeoJSON per animation frame (`OVERLAY_STYLES`, one source
  per kind). The tab fits the map to the trajectories and restores the default view on leaving
  it; Clear pairs and Clear areas/zones now clear only their own kind.
- WPF demo uses `RxSpatial.Geo.GeoPoint` for every map coordinate (the sample's own record was
  deleted); it validates its range, so sentinels like `double.MaxValue` must stay in plain
  doubles (see `MovingObjectManager.LoadTrajectories`). Drawn areas are
  `GeoPolygon.Rectangle`; `PolygonManager.CreatePolygon` returns null (area ignored) for a
  degenerate drag or one wider than 500 km.
- WPF demo map traffic is one web message per dispatcher operation: `MapLibreMap` queues
  `MapCommand`s and posts the batch (a JSON array) from a `DispatcherPriority.Normal` callback
  after the tick or click handler returns. Never post per command again (a tick used to send
  25–70 messages). Upserts with the same (kind, id) replace one another in place; a removal
  nulls the pending upsert and is appended, so per-id order holds. `MarkerCommand` is one
  object per marker and writes only what changed (label once, style when it differs from the
  last one sent, position/reach on a move) — map.html's `setMarker` treats every field as
  optional, and there is no `setMarkerColor` command any more. Outlines (reach, lens, zone
  overlap) travel as integer micro-degree deltas (`OutlineWriter.WriteRing` ↔ `decodeRing`),
  bit-identical to the old `Math.Round(x, 6)` decimals (10M-value check: 0 mismatches).
- WPF demo map.html caps *data-driven* repaints at 60/s by wrapping `map.triggerRepaint`
  (full rate while `map.isMoving()`). MapLibre repaints every frame while a GeoJSON source
  reloads, and this laptop's display runs at 240 Hz, so Overlap/Convoy redrew ~220×/s and the
  renderer and GPU processes each burned >1 core. Render-bound costs therefore scale with the
  display refresh rate (Windows dynamic refresh changes it between sessions: the same baseline
  measured 41% then 120% renderer CPU) — always run baseline and candidate back-to-back and
  record the rAF rate alongside.
- WPF demo replay timer keeps its own `Stopwatch` schedule (`MovingObjectManager.OnTimer`):
  DispatcherTimer fires on the 15.6 ms system tick, so a fixed 40/20 ms interval gave 21/32
  updates/s for a requested 25/50. A late firing runs the due tick(s) (max 3) and shortens
  the next interval.
- WPF demo metrics: the `MapRunRxSpatial` meter (`DemoMetrics`: tick and send duration
  histograms and busy ms, ticks, pushes, web messages and chars). `dotnet-counters collect -n
  MapRunRxSpatial --counters System.Runtime,MapRunRxSpatial --format csv`. Tick time excludes
  the batch send since batching (`maprun.web.send.*`); compare tick+send, or UI-thread CPU.
  Measure UI-thread CPU with cycle counters (`QueryThreadCycleTime`), not
  `TotalProcessorTime`: Windows charges thread time per 15.6 ms clock tick, and a ~1.5 ms burst
  right after each timer interrupt is almost never charged. The UI-automation perf harness
  (scenarios, CDP page metrics, soak) lives outside the repo in `%TEMP%\rxspatial-wpf-perf`.
- The WPF demo requires `IsVisualized=true` (WebView2 must be driven from the UI thread and
  buffered mode notifies from the pool); `App.OnStartup` forces it over appsettings.json.
- Residual uncovered `RStarCore` lines are defensive-by-invariant (single-chain condense,
  roots keep ≥2 children, cleaning ring never empties) — don't chase them with tests.
- Backends own the location-tracker subscription they open in `Subscribe(observer, source)`
  and must dispose it in `Remove`; otherwise the tracked object's next push re-registers the
  observer (`Update` is an upsert). Grid `placements` likewise records the *whole* cell block
  of every observer, region observers included, or unsubscribe and re-indexing silently
  misbehave. `BackendSubscriptionTests` guards both.
- Rx's `Observable.Create` detaches an observer on dispose, so subscription-lifetime bugs are
  invisible through `RxGeographyObservable` — test them on the raw `Rx*`/`RxRx*` API.
- The analyzer bar is `AnalysisMode=Recommended` plus CA1305/CA2000/CA5392/CA1852; new
  exemptions belong in `.editorconfig` with the reason, not in the code. Verify an analyzer
  change with a real recompile (`--no-incremental` or after an edit) — an up-to-date build
  reports nothing and reads as clean.
- Package versions live only in `Directory.Packages.props` (CPM). The MSTest/MTP stack is the
  exception by design: `MSTest.Sdk` in `global.json` is its single version knob — under CPM the
  SDK emits its own `PackageVersion` items, so never add `PackageVersion Include`/`Update` for
  `MSTest.*` or `Microsoft.Testing.*` anywhere (Include duplicates → NU1506; Update in a csproj is
  a no-op because the SDK's items are declared after the project body). Visual Studio's
  package-update UI writes exactly those `Update` blocks into the test csproj — delete them.
- Experiments harness (on Geo since history item 12): a List update over 2,000 `GeoCircle`
  fences costs ~67 µs (it was ~110 ms with SQL Server's ~50 µs `STIntersects`), so every
  variant is now sub-ms and the paper's List/Grid-vs-tree gaps sit inside thread-pool dispatch
  noise; the SQL-backed run is recorded in the README. Its `service` scaling table read ~1×
  only because Windows power-throttled the harness (history item 13); it now opts out and
  shows 7.88× at 20 threads. Every pre-fix multi-object figure on this machine, including the
  SQL-backed ~4× `STIntersects` ceiling, ran throttled — re-run before relying on it.
  Max-load probes must run well past `QueueLimit` rounds (a buffer grows ≤1 per round) and also
  fail on mean latency > interval. Don't build while it runs (locked DLLs; and it measures
  wall-clock).
- Windows power throttling (EcoQoS): a process started from a non-foreground console (an
  agent's or CI shell) slows ~6.7× per core a few seconds into sustained load on this hybrid
  i7-13700H. Any hand-rolled timing loop or harness must call `PowerThrottling.OptOut()`
  (benchmarks/Shared, linked into Benchmarks and Experiments); the tell is a single-thread
  rate that decays over consecutive 2 s cells. BenchmarkDotNet children run at high priority
  and were not throttled (A/B identical); the module initializer still opts them out.
- AVX–SSE transition penalty: a 32-byte struct copy (GeoBounds, ReducedPoint) can make the JIT
  use a 256-bit register; with the upper half dirty, the native `Math.SinCos`/`Math.Tan` that
  follow pay a transition penalty (a 20 ns point-in-circle test measured 295 ns). Keep hot Geo paths
  free of 32-byte copies (bounds are `ref readonly`), and when a short benchmark jumps by
  ~270 ns, rerun with `DOTNET_PreferredVectorBitWidth=128`: if it recovers, this is the cause.
- Geo optimisations must stay bit-identical or justify the change against the fidelity budget.
  The check used in history item 13 is a differential run: a console that prints every public
  operation's result as raw bits over seeded random pairs, built once against a `git worktree`
  of the old commit and once against HEAD, then `cmp`.
- Distance queries return `double` (the papers' signature, not the 2016 `SqlDouble`), NaN when
  no distance exists, so each location update still yields one notification. Core:
  `GeoOperations.Distance` gives NaN for `GeoEmpty` and for a non-converging (nearly antipodal)
  geodesic — the Space Needle's antipode (−47.6205, 57.6507) is the test input; planar NaN only
  for `PlanarEmpty`. RxSql* facades: a Null `STDistance` (different SRIDs, empty geography)
  becomes NaN in `SpatialDistance.ToDouble`. NaN fails every comparison, so classifiers must test
  it first — the demo's `DistanceBands` maps it to `OutOfRange`. The premise tests are
  `Distance_IsNaN_ForTheInputsTheseTestsRelyOn` (core) and the Sql*DistanceQueryTests (SQL Null).
- Two grid settings: `RxSpatialOptions.Grid` (lat/long, `RxSpatial:Grid:*`) serves
  `RxGeography`; `RxSpatialOptions.PlanarGrid` (X/Y, `RxSpatial:PlanarGrid:*`, default 10×10
  over 0..1000) serves `RxGeometry`. Each ops struct picks its own via `GridLayoutOf`.
- `RxGeographyDataStruct` is a historic name: it selects the backend for both types.
- Mixing `RxGeometry` and `RxGeography` (e.g. `geometry.RxRxDistance(geography, …)`) is a
  compile error by design — the concrete facades share no base type.
- Empty shapes pushed into (or subscribed on) the Tree/Grid backends throw: `GeoEmpty`/
  `PlanarEmpty` → `InvalidOperationException` from `Bounds` (pinned by
  `SpatialBackends_RejectEmptyLocations_WhichHaveNoBounds`); `POINT EMPTY` on the RxSql*
  facades throws too (the point fast path reads a Null coordinate). Only the List backend accepts them.
- Cross-platform builds: never build the slnx, `*.SqlServer*` or the WPF project on
  Linux/macOS — build by path (see CI). On Linux `dotnet test` needs
  `--project <csproj>` (MTP mode from global.json). MTP reports the net10.0-windows test DLL
  as `(net10.0|x64)`; that is normal.
- `GeoPolygon`/`PlanarPolygon` reject concave or self-intersecting rings (`ArgumentException`)
  and any shape over 500 km; concave fences need `RxSqlGeography`. Polygon–polygon overlap and
  clipping are local-plane approximations (documented); point/distance-to-polygon are exact.
- The grid's region path is Rect-based since history item 10: the HashMap benchmark rows dropped
  ~5 KB/push at 8dae8d1. A multi-cell push reports each observer once (from the first cell the
  two blocks share), not once per shared cell.
