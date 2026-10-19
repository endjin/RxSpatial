# CLAUDE.md

RxSpatial is the SIGMOD/DEBS 2016 research library for reactive spatio-temporal queries
(`RxGeography` geodetic and `RxGeometry` planar moving objects + continuous
distance/intersect/intersection queries), fully
modernised to .NET 10 / C# 14 / System.Reactive 7 in July 2026. House standards live in
`.endjin/llm-kb/dotnet` (gitignored) — consult before .NET work.

## Commands

```shell
dotnet build RxSpatial.slnx -c Release   # TreatWarningsAsErrors in Release; must stay warning-free
dotnet test -c Release                   # 142 MSTest tests, ~1s (MTP runner, opted in via global.json "test" key)
dotnet run --project samples/RxSpatial.Examples -c Release   # end-to-end smoke: "All scenarios passed."
dotnet run --project benchmarks/RxSpatial.Benchmarks -c Release -- --filter "*TreeIndexBenchmarks*"
```

Layout: `src/RxSpatial` (core), `src/RxSpatial.Index` (R*-tree + RUM-tree, zero deps),
`samples/` (Examples console, MapRunRxSpatial WPF demo), `benchmarks/`, `tests/`,
`docs/prototypes` (never-compiled 2016 archives — do not modernise). Central package
management, artifacts output layout, global.json pins SDK + MSTest.Sdk.

## Architecture essentials

- `RxGeography` (`SqlGeography`) and `RxGeometry` (`SqlGeometry`) are thin public facades over
  one internal engine, `MovingObjectCore<TShape, TOps>` (push queue, drainer, latency, backend
  factory); both implement `IMovingObject<TShape>`. Shape knowledge lives only in
  `ISpatialOperations<TShape>` struct strategies (`GeographyOperations`, `GeometryOperations`)
  with static abstract members, so `TOps.Distance(a, b)` is a JIT-specialised direct call.
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
- Windows-only at runtime (native `SqlServerSpatial170.dll` from Microsoft.SqlServer.Types).

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
   (appsettings.json sets only its start/reset value). The papers (ICDE full paper, DEBS and
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
  exercise tree-backend zone notification push buffered geographies (`.STBuffer(...)`),
  not bare points.
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
  `VehicleColors`, map.html (`MARKER_SHAPES` + CSS) and the legends on all four tabs. Under
  Machado 2009 CVD simulation green/red (deutan 9.1, protan 10.7) and blue/green (tritan 11.5)
  are colour-confusable (ΔE00), gray/green and gray/red weak (<20), so a new state needs a new
  shape, not just a new hue. The triangle
  draws its border as ::before and its fill as ::after (clip-path drops borders and
  box-shadows; the element carries a drop-shadow filter instead).
  Keep the tab header panel `TabNavigation="Once"`: focusing a TabItem selects it, which
  resets the scenario.
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
- `LibraryImport` is not usable here: its generated stub requires `AllowUnsafeBlocks` across
  the assembly, which the single blittable P/Invoke does not justify.
- Distance queries return `double` (the papers' signature, not the 2016 `SqlDouble`): a Null
  `STDistance` (different SRIDs, empty geography) becomes `double.NaN`, converted only in
  `SpatialDistance.ToDouble`, so each location update still yields one notification. NaN fails
  every comparison, so classifiers must test it first — the demo's `DistanceBands` maps it to
  `OutOfRange`. `DistanceQueryTests` pins the premise that those inputs really yield Null;
  `PlanarDistanceQueryTests` pins the same premise for `SqlGeometry` (SRID 0 vs 3857, POINT EMPTY).
- Two grid settings: `RxSpatialOptions.Grid` (lat/long, `RxSpatial:Grid:*`) serves
  `RxGeography`; `RxSpatialOptions.PlanarGrid` (X/Y, `RxSpatial:PlanarGrid:*`, default 10×10
  over 0..1000) serves `RxGeometry`. Each ops struct picks its own via `GridLayoutOf`.
- `RxGeographyDataStruct` is a historic name: it selects the backend for both types.
- Mixing `RxGeometry` and `RxGeography` (e.g. `geometry.RxRxDistance(geography, …)`) is a
  compile error by design — the concrete facades share no base type.
- Empty shapes (`POINT EMPTY`) pushed into the Tree/Grid backends throw for both types (the
  point fast path reads a Null coordinate); only the List backend accepts them.
- The grid's region path is Rect-based since history item 10: the HashMap benchmark rows dropped
  ~5 KB/push at 8dae8d1. A multi-cell push reports each observer once (from the first cell the
  two blocks share), not once per shared cell.
