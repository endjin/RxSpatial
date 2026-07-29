# CLAUDE.md

RxSpatial is the SIGMOD/DEBS 2016 research library for reactive spatio-temporal queries
(`RxGeography` moving objects + continuous distance/intersect/intersection queries), fully
modernised to .NET 10 / C# 14 / System.Reactive 7 in July 2026. House standards live in
`.endjin/llm-kb/dotnet` (gitignored) — consult before .NET work.

## Commands

```shell
dotnet build RxSpatial.slnx -c Release   # TreatWarningsAsErrors in Release; must stay warning-free
dotnet test -c Release                   # 80 MSTest tests, ~1s (MTP runner, opted in via global.json "test" key)
dotnet run --project samples/RxSpatial.Examples -c Release   # end-to-end smoke: "All scenarios passed."
dotnet run --project benchmarks/RxSpatial.Benchmarks -c Release -- --filter "*TreeIndexBenchmarks*"
```

Layout: `src/RxSpatial` (core), `src/RxSpatial.Index` (R*-tree + RUM-tree, zero deps),
`samples/` (Examples console, MapRunRxSpatial WPF demo), `benchmarks/`, `tests/`,
`docs/prototypes` (never-compiled 2016 archives — do not modernise). Central package
management, artifacts output layout, global.json pins SDK + MSTest.Sdk.

## Architecture essentials

- `RxGeography` is `IObserver<SqlGeography>`/`IObservable<SqlGeography>`; observers are held
  in an `ISpatialBackend<T>` (`Backends/`): `ListBackend`, `GridBackend` (spatial hash), or
  `TreeBackend` over `RxSpatial.Index`. Backend chosen by `RxSpatialOptions.PreferredDataStruct`.
- `RxSpatial.Index`: one shared engine (`RStarCore`, struct-generic leaf filters) under two
  facades — `RStarTree` (item-keyed, physical updates) and `RumTree` (update-memo, lazy
  deletion, cleaning token over a circular leaf ring). Correctness anchor:
  `SpatialIndexOracleTests` (seeded randomized workloads vs naive-scan oracle) — keep green
  through any engine change.
- `RxGeographyObservable` exposes the query API as Rx.NET streams via C# 14 extension
  members (`extension(RxGeography source)`).
- Windows-only at runtime (native `SqlServerSpatial170.dll` from Microsoft.SqlServer.Types).

## Modernisation history (July 2026, per-phase commits on master)

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
- MapLibre zoom uses the 512px-tile convention (one level lower than Bing/Leaflet).
- Residual uncovered `RStarCore` lines are defensive-by-invariant (single-chain condense,
  roots keep ≥2 children, cleaning ring never empties) — don't chase them with tests.
