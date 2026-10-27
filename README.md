# RxSpatial

The spatial libraries that have been developed by Microsoft, IBM and Oracle have substantially changed the capabilities of geospatial computing. These libraries implement several functionalities that include intersection, distance, and area for various geospatial objects. These libraries came out to address a wealth of use cases that were challenging in that era. As time goes by, GPS devices and location-aware mobile technologies increased the demand for geospatial computing, in general, and for real time geostreaming, in particular. Existing commercial spatial libraries were originally designed to support operations on stationary objects with limited or no capabilities for moving objects. In this work, we introduce the RxSpatial library, a real time reactive spatial library for spatiotemporal stream query processing. RxSpatial provides, (1) a front-end, which is a programming interface for developers who are familiar with the Microsoft .NET Reactive framework and the Microsoft SQL Server Spatial Library, and (2) a back-end for processing spatial operations in a streaming fashion. RxSpatial provides the programming convenience at the front end and the query processing efficiency at the back end.

## Citation

- Abdeltawab M. Hendawi, Youying Shi, Hossam Fattah, Jumana Karwa, Mohamed H. Ali. *RxSpatial: a framework for real-time spatio-temporal operations.* In ACM DEBS 2016, 366-367
- Youying Shi, Abdeltawab M. Hendawi, Hossam Fattah, Mohamed H. Ali. *RxSpatial: Reactive Spatial Library for Real-Time Location Tracking and Processing.* In ACM SIGMOD 2016, 2165-2168
- Youying Shi, Abdeltawab M. Hendawi, Jayant Gupta, Hossam Fattah, Mohamed H. Ali. *RxSpatial: the reactive spatial library.* In ACM SIGSPATIAL GIS 2016, 64
- Abdeltawab M. Hendawi, Jayant Gupta, Youying Shi, Hossam Fattah, Mohamed H. Ali. *The Microsoft Reactive Framework Meets the Internet of Moving Things.* In IEEE ICDE 2017, 1150-1161 ([doi:10.1109/ICDE.2017.159](https://doi.org/10.1109/ICDE.2017.159)) — the full paper; the map demo's scenarios and default settings follow its figures

A copy of the SIGMOD demo paper is included as [docs/RxSpatialDemo.pdf](docs/RxSpatialDemo.pdf).

## Repository layout

The repository root holds the build and CI files and the documentation; everything .NET (the solution, the shared `Directory.Build.props` and `Directory.Packages.props`, and the projects below) lives under `solutions/`. Open [`solutions/RxSpatial.slnx`](solutions/RxSpatial.slnx). The SQL Server projects are archived in `solutions/archive/`, outside `RxSpatial.slnx`, so neither a solution build nor CI touches them; open or build them with [`solutions/archive/RxSpatial.Archive.slnx`](solutions/archive/RxSpatial.Archive.slnx).

| Path | Platforms | Description |
| --- | --- | --- |
| `solutions/RxSpatial` | Windows, Linux, macOS | The core library: `RxGeography` (geodetic) and `RxGeometry` (planar) moving objects, continuous distance / intersection / intersect queries, and pluggable spatial index backends (list, grid, R*-tree, RUM-tree). |
| `solutions/RxSpatial.Geo` | Windows, Linux, macOS | The shapes and shape maths the core uses: WGS84 points, exact circles and convex polygons (`GeoShape`), their planar twins (`PlanarShape`), and `GeoOperations` / `PlanarOperations` (intersects, intersection, distance, area, bounds). Clean-room, zero dependencies. |
| `solutions/RxSpatial.Index` | Windows, Linux, macOS | R*-tree (Beckmann et al., 1990) and RUM-tree (R-tree with Update Memo, Xiong &amp; Aref) spatial indexes sharing one engine; a clean-room .NET 10 replacement for the formerly vendored LGPL Java ports. Zero dependencies. |
| `solutions/archive/RxSpatial.SqlServer` | Windows only (archived) | `RxSqlGeography` and `RxSqlGeometry`: the same moving objects over Microsoft.SqlServer.Types' `SqlGeography` / `SqlGeometry`, for code that must keep SQL Server's types or results (see [SQL Server types](#sql-server-types-rxspatialsqlserver)). |
| `solutions/RxSpatial.Examples` | Windows, Linux, macOS | Runnable example / smoke test: Rx.NET v7 sequences feeding and consuming RxSpatial streams on every backend. |
| `solutions/MapRunRxSpatial` | Windows only (WPF) | WPF demo that visualises moving objects and continuous queries on a MapLibre GL JS map hosted in WebView2 (OpenStreetMap tiles, no API key). |
| `solutions/RxSpatial.Benchmarks` | Windows, Linux, macOS | BenchmarkDotNet suite: per-backend location-update cost, the RxSpatial.Geo operations, geodesic primitives, polygon construction, outlines and multi-core scaling, plus R*-tree vs RUM-tree bulk-insert / update / query comparisons with allocation statistics. |
| `solutions/archive/RxSpatial.SqlServer.Benchmarks` | Windows only (archived) | The SQL Server baselines: the per-backend workloads through `RxSqlGeography` / `RxSqlGeometry`, and each shape operation on SQL Server against RxSpatial.Geo. |
| `solutions/RxSpatial.Experiments` | Windows, Linux, macOS | Wall-clock harness that reproduces the ICDE 2017 paper's experiments (Figs. 6–12): latency, maximum load and dropped updates under paced location-update streams. |
| `solutions/RxSpatial.Tests` | Windows, Linux, macOS | MSTest (MSTest.Sdk + Shouldly) tests for the core, the indexes and RxSpatial.Geo, including GeographicLib oracle tests and the fidelity tests against the recorded SQL Server values in `solutions/RxSpatial.Tests/Golden`. |
| `solutions/archive/RxSpatial.SqlServer.Tests` | Windows only (archived) | Tests for `RxSqlGeography` / `RxSqlGeometry`, the recorder of `solutions/RxSpatial.Tests/Golden/*.json`, and a guard that live SQL Server still matches it. |
| `docs/prototypes` | — | Never-compiled 2016 prototype sources, archived with provenance notes. |

## Getting started

Requirements:

- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- **The core is cross-platform.** `RxSpatial`, `RxSpatial.Geo` and `RxSpatial.Index` run on Windows, Linux and macOS, and have no native or SQL Server dependency. CI builds and tests them on all three.
- **`RxSpatial.SqlServer` and the WPF demo need Windows.** SQL Server's spatial operations run in the native `SqlServerSpatial170.dll`, which the `Microsoft.SqlServer.Types` NuGet package ships for win-x64, win-x86 and win-arm64 only. The WPF map demo also needs the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (preinstalled on Windows 11) and internet access for the MapLibre GL JS CDN and OpenStreetMap tiles.

On Windows, build and test the solution (everything except the archived SQL Server projects):

```shell
dotnet build solutions/RxSpatial.slnx
dotnet test --solution solutions/RxSpatial.slnx
dotnet run --project solutions/RxSpatial.Examples
```

On Linux or macOS, build the cross-platform solution filter `solutions/RxSpatial.CrossPlatform.slnf` (the solution also holds the Windows-only projects):

```shell
dotnet build solutions/RxSpatial.CrossPlatform.slnf
dotnet test --solution solutions/RxSpatial.CrossPlatform.slnf
dotnet run --project solutions/RxSpatial.Examples
```

To run the same build as CI, see [Build and CI](#build-and-ci).

The examples program drives a vehicle towards a geofence around the Seattle Space Needle, and a planar `RxGeometry` forklift towards a loading bay, and asserts the behaviour of every spatial index backend; it doubles as an end-to-end smoke test. The same scenarios are covered as unit tests in `solutions/RxSpatial.Tests`.

To run the benchmarks (BenchmarkDotNet requires a Release build):

```shell
dotnet run --project solutions/RxSpatial.Benchmarks -c Release -- --filter "*"
dotnet run --project solutions/archive/RxSpatial.SqlServer.Benchmarks -c Release -- --filter "*"   # Windows only, archived: the SQL Server baselines
```

`SpatialBackendBenchmarks` measures the cost of pushing one location update through `RxGeography` per index backend — the list backend scales linearly with the observer count while the grid and tree backends prune by zone. `PlanarBackendBenchmarks` runs the same workload through `RxGeometry` on a 1,000 × 1,000 floor. `solutions/archive/RxSpatial.SqlServer.Benchmarks` (Windows) runs both workloads through `RxSqlGeography` / `RxSqlGeometry` as the SQL Server baseline. In one short run at 1,000 observers (2 km zone discs, 50 m push discs), a push cost:

| Backend | `RxGeography` (Geo) | `RxSqlGeography` (SQL Server) | Planar: `RxGeometry` / `RxSqlGeometry` |
| --- | --- | --- | --- |
| List | 6.6 µs | 107 ms | 2.8 µs / 1.4 ms |
| Grid (HashMap) | 115 ns | 1.4 ms | 105 ns / 36 µs |
| R\*-tree | 71 ns | 78 µs | 79 ns / 11.6 µs |
| RUM-tree | 74 ns | 77 µs | 91 ns / 11.2 µs |

Allocation per push is 40–116 B on Geo, against 29–328 KB (geodetic) and 1.2–328 KB (planar) on SQL Server. The List ratio is extreme because Geo rejects most circle–circle pairs by their degree bounds before any geodesic, while SQL Server intersects two 128-gons every time.

`GeoOperationsBenchmarks` times each RxSpatial.Geo operation on its own, and `SqlVsGeoOperationsBenchmarks` (Windows) times the same operations on SQL Server as the baseline of each category. One short run (`--job short`, i7-13700H), on the 500 m Space Needle fence and two 2 km discs 1.5 km apart:

| Operation | SQL Server | RxSpatial.Geo | Geo ÷ SQL | Allocated (SQL / Geo) |
| --- | --- | --- | --- | --- |
| Bounds of a circle ¹ | 18.4 µs | 0.2 ns | — | 29,360 B / 0 B |
| Point in circle, inside | 58.0 µs | 20.2 ns | 0.0003 | 336 B / 0 B |
| Point in circle, 100 m outside ² | 58.4 µs | 1.6 ns | 0.00003 | 336 B / 0 B |
| Disc–disc intersects | 226 µs | 23.3 ns | 0.0001 | 336 B / 0 B |
| Point–point distance | **85.7 ns** | 152 ns | **1.77** | 0 B / 0 B |
| Point–circle distance | 123 µs | 139 ns | 0.001 | 336 B / 0 B |
| Lens (disc ∩ disc) | 190 µs | 21.0 µs | 0.11 | 4,304 B / 6,280 B |
| Area of the lens ¹ | 113 µs | 0.4 ns | — | 168 B / 0 B |

¹ A Geo shape computes its bounds (and a polygon its area) when it is constructed, so these rows read a field: the work moves to the constructor. SQL Server's bounds row is the WKB-envelope path that `RxSqlGeography`'s tree and grid backends take. ² Rejected by the bounding box before any geodesic. SQL Server stays faster for a single point–point distance (Geo iterates Vincenty's solution to 1e-14), and the lens allocates more on Geo, because it builds a polygon with its arc vertices.

The table above was recorded before the optimisation pass described next; the Geo column is now faster on every row it shares with that pass.

#### RxSpatial.Geo optimisation pass (October 2026)

More benchmarks now cover the geodesic primitives (`GeodesyBenchmarks`: Vincenty inverse and direct, `FastDistance`, `LocalPlane.Project` / `Unproject`), polygon construction (`ShapeConstructionBenchmarks`: `Rectangle`, `FromVertices`, `FromWkt` at 4 and 64 vertices, and the convexity validation on its own), outlines (`OutlineBenchmarks`: `ToVertices` at 64 and 128 segments, which the WPF demo calls on every update for each reach circle) and multi-core throughput (`GeoScalingBenchmarks`, below). A profile of these runs led to four changes. Each change gives bit-identical results: a 100,000-pair differential run of every public operation matched the earlier build bit for bit, and the golden fidelity, GeographicLib oracle, property and allocation tests pass unchanged.

- **Longitude wrapping without `Math.IEEERemainder`.** The remainder returns its argument unchanged in [−180°, 180°], so that range now skips the helper call (about 3 ns of the 12.6 ns `FastDistance`).
- **Bounds read in place.** A copy of the 32-byte `GeoBounds` let the JIT use a 256-bit register. Its dirty upper half then caused an AVX–SSE transition penalty in the native `Math.SinCos` that `FastDistance` calls, and point-in-circle tests ran at ~295 ns instead of 20 ns. Shapes now expose their bounds by `ref readonly`.
- **Reduced latitude worked out once per start point** (`ReducedPoint`), when many geodesics share a start: projecting a ring, polygon construction, intersection results and circle outlines.
- **Fewer exact geodesics for polygons.** The distance to a polygon measures only the edges that can be nearest, which the polygon's own plane shortlists within a proven error bound. Circle–polygon `Intersects` decides in that plane when the plane distance is clear of the radius, instead of running `Contains` twice and measuring every edge. Point-in-polygon tries the flat-earth offset first, within a bound of twice its worst measured error, and runs the Vincenty projection only near an edge.

`GeoOperations.ToVertices` and `PlanarOperations.ToVertices` also gained overloads that write into a `Span` and allocate nothing. Same machine, same session, default BenchmarkDotNet job, the baseline run before and after the optimised one (the before column is their mean; no row drifted by more than 1% between them except the noisy sub-100 ns backend rows):

| Benchmark | Before | After | After ÷ before |
|---|---:|---:|---:|
| Point in circle, inside (guarded) | 20.2 ns | 13.9 ns | 0.69 |
| Point in polygon, inside | 54.0 ns | 27.6 ns | 0.51 |
| Disc–disc intersects | 23.3 ns | 17.3 ns | 0.74 |
| Circle–polygon intersects | 1,036 ns | 173 ns | **0.17** |
| Circle–polygon distance | 880 ns | 558 ns | 0.63 |
| Point–polygon distance | 887 ns | 738 ns | 0.83 |
| Lens (disc ∩ disc) | 20.96 µs | 18.64 µs | 0.89 |
| Disc ∩ rectangle | 10.28 µs | 8.39 µs | 0.82 |
| `FastDistance` | 12.6 ns | 9.9 ns | 0.79 |
| `Wgs84.Direct` | 168 ns | 155 ns | 0.93 |
| `ToVertices`, 2 km circle, 64 segments | 10.57 µs / 1,048 B | 9.03 µs / 1,048 B (span overload: 8.96 µs / 0 B) | 0.85 |
| `GeoPolygon.FromVertices`, 64 vertices | 15.86 µs | 14.60 µs | 0.92 |
| Mixed Distance/Intersects, 1 thread | 290 ns | 248 ns | 0.85 |

Vincenty's inverse itself (131 ns at 1.4 km, 176 ns at 230 km) and the backend push rows did not change beyond noise: a push is dominated by the index and the observers, not by the maths. Two rows read slower, and neither is a regression in the maths. The `Bounds` rows went from "indistinguishable from an empty method" to 0.2 ns, but a re-check with three launches of each build gave the reverse (0.24 ns before, 0.003 ns after), so this is BenchmarkDotNet's zero-measurement floor. `PlanarPolygon.FromVertices` with 4 vertices read 88 → 94 ns, and 88.7 → 92.7 ns (+4%, within 2σ) in the re-check, although its code path did not change: this is code layout, not work.

**Multi-core scaling.** `GeoScalingBenchmarks` runs the shape maths alone on 1–20 dedicated threads. Each thread pulls one route point at a time from a padded shared counter and tests it against shared fences. Results are kept in locals and published once, into padded slots. On the i7-13700H (6 performance cores with hyper-threading and 8 efficiency cores, 20 logical processors):

| Threads | 2,000-fence `Intersects` (Mops/s, speed-up) | Mixed Distance/Intersects (Mops/s, speed-up) |
|---:|---:|---:|
| 1 | 205 (1.00×) | 4.0 (1.00×) |
| 2 | 436 (2.12×) | 8.0 (1.99×) |
| 4 | 730 (3.56×) | 14.6 (3.63×) |
| 8 | 966 (4.71×) | 21.7 (5.38×) |
| 14 | 1,079 (5.26×) | 28.9 (7.17×) |
| 20 | 1,191 (5.81×) | 33.8 (8.37×) |

The maths scales. The compute-heavy mix reaches 8.4× at 20 threads. The cheap fence loop (about 5 ns an operation, mostly the bounding-box test) reaches 5.8×, because it is limited by memory more than by arithmetic. Past 6 threads the extra threads are hyper-threads and efficiency cores, so the speed-up is sub-linear on this hardware.

`TreeIndexBenchmarks` compares the R*-tree (the per-category baseline) against the RUM-tree for bulk insertion, the frequent-update workload the RUM-tree was designed for, and intersection queries (including the allocation-free buffered `Search` overload). Because both trees share the same R* engine, the measured delta is purely the update-memo strategy. After profile-guided optimisation of the engine (a containment fast path in ChooseSubtree found via dotnet-trace, and pooled reinsertion scratch found via dotnet-dump heap analysis), the paper's thesis holds in memory too: RUM-tree updates run at roughly 0.6× the cost of an R* remove-and-reinsert (~0.6 µs vs ~1.0 µs at N=1000, and ~375× the original vendored port's 229 µs) with near-zero steady-state allocation. Coverage-driven `Churn` (delete + reinsert) and `UpdateLocal` (small-delta tracking moves) categories then motivated a bottom-up fast path: when a moved entry still fits its leaf's recorded cover it is rewritten in place, dropping local moves to ~0.42× the cost of a teleporting update with ~zero allocation — so the R*-tree now wins tracking workloads while the RUM-tree wins teleport-heavy ones.

### Reproducing the ICDE 2017 experiments

The BenchmarkDotNet suite times single operations. The paper's §VI measures a running system instead: average latency from the input buffer to the output observers, the maximum number of moving objects that can be tracked without dropping an update, and the number of dropped updates. `solutions/RxSpatial.Experiments` reproduces those measures with real, paced streams (one high-priority producer thread, Stopwatch deadlines with sleep-then-spin waits, and achieved rates reported next to the requested ones). It runs in buffered mode (`IsVisualized=false`) with an input buffer (`QueueLimit`) of 10. A dropped update is a push that throws `RxSpatialOverloadException`, counted in the harness.

```shell
dotnet run --project solutions/RxSpatial.Experiments -c Release -- all          # every figure, quick scale (~45 min)
dotnet run --project solutions/RxSpatial.Experiments -c Release -- fig6 --full  # the paper's update counts and every axis point
```

The subcommands are `service`, `fig6` … `fig12` and `all`. Options: `--out <dir>` (default `solutions/artifacts/experiments/<timestamp>`), `--seed <n>` (default 42) and `--max-objects <n>` (the maximum-load cap: 1,024, or 4,096 with `--full`). Each figure writes a CSV, and `summary.md` holds one table per figure next to the paper's claims. The workload is deterministic: 2,000 exact `GeoCircle` geofences of 100–400 m in the Seattle area (three quarters clustered round 25 hot spots, as malls and points of interest are) and a random-walk route of bare points for each moving object. The paper's "SqlSpatial" baseline is the **List backend**. The paper itself calls it "List based (SQLSpatial)" indexing, and a plain `GeoOperations.Intersects` loop over the same fences, with no RxSpatial at all, costs the same within 8% (the `service` table; it was also within 8% of a plain `STIntersects` loop when the harness used SQL Server).

**The harness now runs on RxSpatial.Geo, on any operating system, and that changes the comparison with the paper.** The paper's baseline is slow because SQL Server's spatial library is slow: a geography `STIntersects` of a point against an `STBuffer` polygon costs ~50 µs. On RxSpatial.Geo the same test costs nanoseconds, so the List baseline speeds up about 1,600× and the gaps the paper reports between List, Grid and the trees fall into the thread-pool dispatch noise of the buffered drainer (~0.2–2 ms). A quick re-run (`service fig6 fig9 fig11`, default scale, same machine, one run each) against the earlier SQL-backed run:

| Measure | With SQL Server's types (earlier run) | On RxSpatial.Geo |
| --- | --- | --- |
| `service`: one update, 2,000 fences, List / Grid / R-tree / RUM-tree | 109 ms / 80 ms / 6.2 µs / 18 µs | 67 µs / 55 µs / 0.52 µs / 0.61 µs |
| Fig. 6: RUM-tree vs SqlSpatial latency at 200 ms | 175× lower (0.63 vs 110 ms) | 1.5× lower (0.83 vs 1.27 ms): every variant is 0.2–6 ms, noise-dominated |
| Fig. 9: at 100 observers, RUM-tree vs SqlSpatial | 87% lower | 6% lower; Grid lowest — no ordering survives the noise |
| Fig. 11: drops at 5–40 ms, SqlSpatial / Grid | 122–181 and 100–164 of 200 at every point | none from 2 ms upwards, for every variant |

So, on Geo, the paper's qualitative claims about the baseline no longer hold on this hardware: the indexes still prune 2,000 fences to 0.25 evaluations per update, but the work they save is a few tens of microseconds. The paper's comparison is reproduced only with SQL Server's spatial types, and those figures, recorded before the move to RxSpatial.Geo, are kept below. The harness has no SQL Server option: it would need a second moving-object type through the whole harness, and `SqlVsGeoOperationsBenchmarks` already measures the per-operation difference.

Recorded with SQL Server's spatial types (Microsoft.SqlServer.Types 170.1000.7), before the move to RxSpatial.Geo:

| Figure | Paper claim | Measured, default scale (one run, 20 logical processors) | |
| --- | --- | --- | --- |
| 6 — latency vs inter-arrival, 1 object × 2,000 fences | RUM ~55× lower than SqlSpatial at 200 ms; 13.3% / 41.7% lower than R-tree / Grid | RUM **175×** lower than SqlSpatial at 200 ms (0.63 vs 110 ms); `--full` (50 updates per point): **238×**, SqlSpatial backlogged to 647 ms at 100 ms (its ~120 ms update outlasts the interval), RUM 99% below Grid and on average 18.7% below the R-tree (lower at 6 of 9 points, but both trees sit at 0.2–1.4 ms of thread-pool dispatch noise) | ordering and shape reproduced; ratio exceeded; R-tree vs RUM within noise |
| 7 — max load vs inter-arrival | fewer objects at shorter intervals; SqlSpatial lowest; trees above Grid | SqlSpatial 8 → 60, Grid 14 → 99 (200 → 1,500 ms), linear in the interval; both trees never fail up to the 1,024-object cap | reproduced; R-tree vs RUM not resolvable |
| 8 — max load vs observers per object (20 ms) | Grid 100 → 90, trees 105 → 95, SqlSpatial 80 → 40 | SqlSpatial 60 → 10, Grid 176 → 19 (20 → 160 observers); trees ≥1,024 | ordering reproduced; List/Grid fall much more steeply |
| 9 — latency vs observers (1 object, 200 ms) | at 100 observers RUM 11.2% / 62.8% / 94.2% lower than R-tree / Grid / SqlSpatial | 2.7% / 85% / 87% lower | partially (tree vs tree is noise) |
| 10 — latency vs objects (500 fences, 1 s) | comparable at 200 objects; at 1K RUM 12.2% / 37.2% / 55% lower | SqlSpatial 0.6 s → 14.8 s and Grid 25 ms → 9.5 s (saturated); trees ≤ 2 ms throughout | trend reproduced, gaps far wider |
| 11 — drops vs inter-arrival, 5–40 ms | no drops above 15 ms (R-tree) / 10 ms (RUM); SqlSpatial most, Grid next | SqlSpatial 122–181 and Grid 100–164 of 200 dropped at every point; trees drop nothing at 2 ms and above, and at most 11 of 200 at 0.1–1 ms | reproduced; the trees' threshold is now ~1 ms |
| 12 — drops vs objects (1 → 20 × 200 requests × 50 observers, 50 ms) | trees drop 190–200% fewer than SqlSpatial and Grid | no variant drops anything; the deepest input buffer seen is 0 | not reproduced: the load is ~25% of this machine's capacity |

How to read these results:

- **Why the trees win by orders of magnitude, not by the paper's factors.** A geography `STIntersects` of a point against an `STBuffer` polygon still costs ~50 µs in Microsoft.SqlServer.Types 17.0. With 2,000 fences, an update costs ~110 ms on the List variant, which is in line with the paper's 2016 figures. The clean-room trees, however, evaluate only the 0.25 fences whose bounding box contains the point, so an update costs 6–18 µs and the measured latency (~0.2–2 ms) is almost entirely the thread-pool dispatch of the drainer. Neither the R-tree vs RUM-tree latency gap (13.3%, 11.2%, 12.2% in the paper) nor their maximum-load gap (3.7%) can be resolved: both are below this noise floor, and the trees outrun the object cap. The in-memory comparison of the two trees is `TreeIndexBenchmarks`.
- **The multi-object figures did not scale with the cores, because Windows throttled the harness.** The `service` scaling table read ~1× from 2 to 20 threads on RxSpatial.Geo, while a research spike on the same machine measured 7.5×. Windows 11 power-throttles (EcoQoS) a process that it judges to be in the background, such as one started from a console that is not the foreground window. On this hybrid laptop (i7-13700H, Balanced power plan, on AC power), a throttled process slows about 6.7× per core about 4 s into sustained load: one thread of Vincenty distances fell from 6.0 to 0.9 million/s, and per-core utility fell from ~100% to ~12% while the thread stayed 100% busy. The harness measured one thread first, while it was still fast, and every later cell throttled. The spike ran 10 s of SQL Server cells first, so every one of its clean-room cells was throttled alike, and its ratios looked clean. The harness now opts out with `SetProcessInformation(ProcessPowerThrottling)` (set `RXSPATIAL_ALLOW_POWER_THROTTLING=1` to keep the default). Its scaling table also reports busy cores and repeats the single-thread run at the end, so that any drift shows. Same session: **7.88× at 20 threads**, with the repeat at 1.03, against 0.97× and a repeat at 0.14 with throttling allowed. `GeoScalingBenchmarks` gives 5.8× (fence loop) to 8.4× (mixed maths). BenchmarkDotNet's child processes were not affected: they run at high priority, which Windows does not throttle, and an A/B run measured identical times. Every earlier harness figure on this machine ran throttled, including the SQL-backed ~4× `STIntersects` ceiling, so re-run any multi-object figure before you rely on it.
- **The Grid is the default 10 × 10 grid over Washington State**, so all of Seattle falls in two cells and the grid evaluates ~1,500 of the 2,000 fences per update. This is the degeneration into "a small number of linear lists" that the paper describes.
- **What the paper leaves out, and what the harness assumes:** the per-object workload of Figs. 7, 8 and 10 (2,000, N and 500 fences), the interval of Figs. 8–10 (20 ms, 200 ms and 1 s), the update count per point, the input buffer outside Fig. 12 (10 everywhere), the observer type of Fig. 12 (`RxIntersect`), the fence shapes and their distribution (the paper used MNTG-generated data that it did not publish), and the hardware beyond "Windows 8.1". A maximum-load probe of 20 (or 50) updates per object passes only if it drops nothing *and* its mean latency stays below the interval. A backlogged system has not dropped yet only because the probe is finite, and it would drop on a longer run.
- `RxGeography.AverageLatency` now records total, fractional milliseconds. The 2016 code averaged the integer `TimeSpan.Milliseconds` component, which read every sub-millisecond update as 0 and wrapped latencies over one second, so Fig. 6(b) could not be measured on current hardware.

### Running the map demo

```shell
dotnet run --project solutions/MapRunRxSpatial
```

![The MapRun RxSpatial demo on the Geofence tab. The control panel on the left has the traffic controls, the area drawing buttons, the spatial index switch and the guidance for the tab. The map of Seattle on the right shows a purple dashed confinement area, a hatched restricted area and 20 numbered vehicles. Vehicles inside the restricted area or outside the confinement area are red diamonds, and the others are blue circles.](https://res.cloudinary.com/endjin/image/upload/v1792916244/assets/images/blog/2026/10/maprun-rxspatial.jpg)

The control panel has one tab per scenario — **Geofence** (`RxIntersect`), **Near area** (`RxDistance`), **Near vehicle** (`RxRxDistance`), **Convoy link** (`RxRxDistance` with a connection line) and **Overlap** (`RxRxIntersect`, `RxRxIntersection` and `RxIntersection`) — and each tab shows its own steps and colour legend. On *Geofence*, the **Spatial index** buttons (List, Grid, R\*-tree, RUM-tree) switch the index while the demo runs: the vehicles are rebuilt on the new index and the drawn areas stay. The tab starts with, and a tab switch restores, the index set in `appsettings.json` (the **RUM-tree by default**). The alerts are the same on every index, because the queries are exact and the index only prunes which observers are evaluated; the papers compare the indexes on throughput and latency at scale (ICDE 2017 Figs. 6, 7 and 12), which 20 vehicles cannot show. The other tabs always use the List index. Spatial pruning would suppress their continuous distance streams, and on *Overlap* the results that say two reaches no longer overlap or a reach has left a zone. The panel footer shows the index in use.

Each tab starts with the settings of the matching figure in the RxSpatial papers, so pressing **Start** reproduces it. Nothing moves until you press **Start**, and switching tabs restores these defaults:

| Tab | Vehicles | Observe distance | Watched pairs | Reproduces |
| --- | --- | --- | --- | --- |
| *Geofence* | 20 | — | — | DEBS 2016 Fig. 5 |
| *Near area* | 20 | 4,600 m | — | DEBS 2016 Fig. 4 (family locator, 4,610.93 m) |
| *Near vehicle* | 20 (fixed) | 5,000 m | 10–12, 15–16 | SIGMOD 2016 control panel (5,065.43 m); ICDE Fig. 4c pairs |
| *Convoy link* | 20 (fixed) | 8,000 m | 4–18, 10–12, 7–9 | ICDE Fig. 5 / DEBS 2016 Fig. 3 |
| *Overlap* | 20 (fixed) | — (Reach: 2,000 m) | 15–16, 10–12, 7–9, 1–17 | DEBS 2016 §3.3 (car pooling, Fig. 3) and §3.1 |

**Replay rate** is 25 updates/s on every tab. The papers do not give a consistent value (their screenshots show 1, 48 and 56 on a differently scaled slider), so this is a readable middle value. The replay keeps its own clock, so the vehicles really move at the rate the slider shows (up to 50 updates/s); a plain WPF timer, which fires only on Windows' 15.6 ms clock tick, delivered 21 and 32 updates/s for 25 and 50. The papers give no area coordinates, so on the area tabs you draw them: press **Add area** (on *Geofence*, **Restricted area** or **Confinement area**) and drag a rectangle on the map. The button stays pressed while the map is in drawing mode, which shows a crosshair and a hint; the drag draws instead of panning, while the mouse wheel and the +/- buttons still zoom. Esc, a right-click or pressing the button again cancels. Outside drawing mode the map pans and zooms as usual, and clicks do nothing. *Geofence* has both kinds of zone from the restricted/confinement zone alert of ICDE 2017 Fig. 4a: an offender must stay out of every **restricted** area (slate hatching) and inside a **confinement** area (purple dashed outline) (a city or county; the DEBS 2016 §3.4 family locator's designated region for a child). Being inside any one confinement area is enough. The pre-set pairs are watched as soon as their vehicles appear after **Start**. To watch more pairs, choose a **First vehicle** and a **Second vehicle** and press **Watch pair**. On *Near vehicle*, **Watch all pairs** watches all 190 pairs: the self-join `RxRxDistance` of ICDE 2017 §V.B, which continuously evaluates the distance between every pair of cars (380 observers, because each pair is watched from both ends). Each vehicle then shows the colour of its nearest other vehicle. *Convoy link* does not offer this, because 190 connection lines would be unreadable. **Clear pairs** removes every pair, the pre-set pairs too, until the next tab switch.

The *Convoy link* figure shows 9,935 m. On the recorded trajectories, pair 4–18 is beyond that distance only for the first few updates, so it would turn green almost at once. At 8,000 m, the pairs stay in the states the figure describes for most of the replay: 4–18 is gray (beyond the limit) 53% of the time, 10–12 is yellow (stable) 94% of the time, and 7–9 is green (near the limit) more often than any other colour (40%).

*Overlap* shows the three intersection queries that the other tabs do not use. On this tab each vehicle pushes its **reach** instead of a bare point: `new GeoCircle(position, reach)`, an exact disc with the **Reach** radius (250 m to 3 km). The queries therefore compare regions. The marker stays at the vehicle's own position, and each reach is drawn as a thin slate circle through the vertices `GeoOperations.ToVertices` places on that same circle. The map zooms to the recorded trajectories so that a reach is wider than a marker, and returns to the default view when you leave the tab.

- **Car pooling** (DEBS 2016 §3.3: "the developer could easily use the RxRxIntersection observer … If the intersection is small till these cars reach their destination like 15 and 16, these cars could car pool"). Each watched pair has an `RxRxIntersect` query, which turns both markers green while their reaches overlap, and an `RxRxIntersection` query. Its result, the **lens**, is drawn from the vertices of the `GeoPolygon` that `GeoOperations.Intersection` returns, and labelled with its exact area (`GeoOperations.Area`) in km².
- **Friends in a region** (DEBS 2016 §3.1: "find all my friends in Seattle downtown"). Press **Add zone** and drag a rectangle. An `RxIntersection` query gives each vehicle the part of its reach inside the zone, which is shaded.

The papers give no reach, so the default comes from the recorded trajectories. At 2,000 m, pair 15–16 overlaps for the whole replay, 10–12 for 90% of it, 7–9 for 24% and 1–17 for 16%; about 11 of the 190 pairs overlap at any moment. The figure's fifth pair, 4–18, is left out, because those vehicles always stay more than 6 km apart. **Watch all pairs** watches all 190 pairs with `RxRxIntersect` only, without lenses, because 190 lenses would cover the map. (With SQL Server's 128-gon discs, intersecting 380 pairs took about 40 ms a tick and halved the replay speed; RxSpatial.Geo tests two exact discs in about 23 ns.) **Clear pairs** and **Clear zones** each remove only their own kind.

Colours, where *d* is the observe distance (a vehicle in several queries shows its most urgent colour):

| Colour (marker shape) | Area tabs (vehicle vs area) | Pair tabs (vehicle vs vehicle; *Convoy link* line) |
| --- | --- | --- |
| Blue (circle) | No alert: outside every restricted area (and inside a confinement area, if any), or farther than *d* from an area | Not watched |
| Gray (dashed ring) | — | Watched, farther apart than *d* (dark dotted line) |
| Green (rounded square) | Within *d* | Within *d*, near the limit (thin line) |
| Yellow (triangle) | Closer than 4/7 *d* | Closer than 4/7 *d*: stable working distance (thick line) |
| Red (diamond) | Inside a restricted area, or outside every confinement area | Closer than 1/7 *d*: the minimum safe distance is breached (thickest line) |

On *Overlap*, a watched vehicle is gray (dashed ring) while its reach overlaps no watched partner, and green (rounded square) while it overlaps at least one. The map overlays on that tab are:

| Overlay | Meaning |
| --- | --- |
| Thin slate circle | A vehicle's reach |
| Teal dot pattern with a solid teal outline, and an area label | The lens: where the reaches of a watched pair overlap (`RxRxIntersection`) |
| Brown outline, labelled *Zone* | A zone that you drew |
| Brown back-diagonal hatching | The part of a vehicle's reach inside a zone (`RxIntersection`) |

Accessibility: the panel meets WCAG 2.2 AA contrast (4.5:1 text, 3:1 control states) in the Fluent light theme, whatever the Windows accent colour (a light accent is replaced with a dark blue). Marker state does not rely on colour alone: each state has its own marker shape (blue circle, gray dashed ring, green rounded square, yellow triangle, red diamond), every area and overlay has its own pattern or outline, and the legends draw and name the same shapes. Drawing an area needs a mouse drag; every other control works from the keyboard and has an accessible name.

## Build and CI

The CI build is the standard endjin [ZeroFailed](https://github.com/zerofailed/ZeroFailed) build for .NET: `build.ps1` runs the process configured in `.zf/config.ps1`, which uses the [ZeroFailed.Build.DotNet](https://github.com/zerofailed/ZeroFailed.Build.DotNet) extension. Run it locally with PowerShell 7:

```powershell
./build.ps1                                                   # full build: compile, test, analysis, package
./build.ps1 -Tasks Build,Test                                 # compile and test only
$env:ZF_BUILD_DOTNET_SOLUTION_TO_BUILD = 'solutions/RxSpatial.CrossPlatform.slnf'
./build.ps1 -Tasks Build,Test                                 # what the Linux and macOS CI jobs run
```

The first run installs the `InvokeBuild` and `ZeroFailed` PowerShell modules from the PowerShell Gallery, downloads the ZeroFailed extensions into `.zf/extensions`, and installs the .NET global tools that the build needs (GitVersion, dotnet-coverage, ReportGenerator, Covenant). The build:

1. **Version:** computes the version with GitVersion (`GitVersion.yml`). Until the first release tag, `main` builds `0.1.0-preview.<n>`.
2. **Build:** restores and builds the solution in Release. Warnings are errors.
3. **Test:** runs `dotnet test` (Microsoft.Testing.Platform) under `dotnet-coverage`, writes TRX files to `TestResults/`, then runs the examples smoke test. After the build, a Cobertura report and a Markdown summary for the four libraries go to `_codeCoverage/`.
4. **Analysis:** writes a [Covenant](https://github.com/patriksvensson/covenant) SBOM (`RxSpatial.sbom.json` and `.html`).
5. **Package:** runs `dotnet pack`, which produces nothing: `Directory.Build.props` sets `IsPackable` to `false`, because RxSpatial is not released as a package. To ship a library, set `IsPackable` to `true` in its project file.
6. **Publish:** turned off (`$SkipPublish` in `.zf/config.ps1`). The build never pushes to a NuGet feed.

GitHub Actions workflows (`.github/workflows`):

| Workflow | Trigger | What it does |
|---|---|---|
| `build.yml` | Push to `main`, tags, pull requests to `main`, manual | **Windows:** the full ZeroFailed build over `solutions/RxSpatial.slnx`, with test results and a coverage comment on pull requests. **Ubuntu and macOS:** `Build,Test` over `solutions/RxSpatial.CrossPlatform.slnf`. All jobs use the endjin `run-build-process` action. |
| `dependabot_approve_and_label.yml` | Pull request opened or reopened | Approves Dependabot PRs for the packages listed in `.github/config/pr-autoflow.json` (patch and minor updates only), turns on auto-merge for them, and labels every PR that is not from Dependabot `pending_release`. |
| `auto_release.yml` | Pull request closed | When no `pending_release` PR is blocked by an open auto-mergeable Dependabot PR, tags the `main` head with the GitVersion version. A PR labelled `no_release` does not cause a release. |

Dependabot (`.github/dependabot.yml`) checks NuGet packages (`Directory.Packages.props`, and `MSTest.Sdk` in `global.json`) and GitHub Actions weekly. The `Microsoft.Extensions.*` packages come in one grouped PR, which is always reviewed by a person.

The workflows need these settings in the GitHub repository or organisation:

| Setting | Kind | Used by |
|---|---|---|
| `SHARED_WORKFLOW_KEY` | Actions secret | `build.yml`: encrypts secrets passed to the build. The build has no secrets yet, so without it the build only logs a warning. |
| `ENDJIN_BOT_APP_ID`, `ENDJIN_BOT_PRIVATE_KEY` | Actions secrets | `auto_release.yml`: a GitHub App token that creates release tags, so that the tag push starts `build.yml`. |
| `DEPENDJINBOT_APP_ID`, `DEPENDJINBOT_PRIVATE_KEY` | Dependabot secrets | `dependabot_approve_and_label.yml`: a GitHub App token that turns on auto-merge, so that the merge starts `auto_release.yml`. |
| `pending_release`, `no_release` | Issue labels | Release automation. |
| Allow auto-merge, and branch protection on `main` that requires the `build.yml` checks | Repository settings | Auto-merge waits for the required checks. Without required checks, an approved Dependabot PR merges at once. |

## Using RxSpatial with System.Reactive v7

`RxGeography` implements `IObserver<GeoShape>` / `IObservable<GeoShape>`, so it composes directly with Rx.NET. The library also ships `RxGeographyObservable` — C# 14 extension members that expose the continuous query API as ordinary observable sequences:

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

Continuous queries between two moving objects (`RxRx*` in the papers) are available through the same extensions: `a.DistanceTo(b)`, `a.Intersects(b)`, `a.IntersectionWith(b)`.

Distance queries (`RxDistance`, `RxRxDistance` and both `DistanceTo` overloads) report metres as `double`, as the papers declare them (`IObserver<double>`). When a distance cannot be computed, the value is `double.NaN`, so every location update still produces exactly one notification. In the core this happens for an empty shape (`GeoEmpty`) and for a nearly antipodal pair of points, where the geodesic solver does not converge; in the `RxSql*` facades it happens when SQL Server's `STDistance` returns Null (different SRIDs, an empty geography). Filter with `double.IsNaN` if your pipeline must not see it.

The papers name the two families by their operands: a single `Rx` prefix (`RxIntersect`, `RxDistance`, `RxIntersection`) relates a moving object to a stationary geography, and a double `RxRx` prefix relates two moving objects. The method names follow the SIGMOD 2016 class diagram; the ICDE 2017 paper spells them `RxIntersects` / `RxRxIntersects`, which are the same operations.

## Planar moving objects: `RxGeometry`

The ICDE 2017 paper (§III) names a second moving-object class beside `RxGeography`: "the same concepts would apply to the RxGeometry class". The authors never released it, so `RxGeometry` here is a clean-room version of that contract. It is the planar sibling of `RxGeography` (over `PlanarShape`), with the same six queries (`RxIntersect`, `RxIntersection`, `RxDistance` and their `RxRx*` forms), the same `Location`, `AverageLatency` and `QueuedPushCount`, and `RxGeometryObservable` extension members that mirror `RxGeographyObservable` (`Locations`, `DistanceTo`, `Intersects`, `IntersectionWith`). Use it for flat coordinate systems: a warehouse or factory floor, a CAD drawing, or a projected map.

```csharp
// A forklift on a warehouse floor (metres) approaching a 100 m loading bay.
var bay = PlanarPolygon.Rectangle(minX: 450, minY: 450, maxX: 550, maxY: 550);
var forklift = new RxGeometry();

using var nearBay = forklift.DistanceTo(bay)
                            .Where(metres => metres < 100)
                            .Subscribe(m => Console.WriteLine($"{m:F0} m from the bay"));

forklift.OnNext(new PlanarPointShape(100, 500));   // 350 m
forklift.OnNext(new PlanarPointShape(400, 500));   // 50 m: reported
```

- **Units.** Distances are Euclidean, in the shapes' own coordinate units (metres in the example), not geodetic metres. The planar maths is exact.
- **NaN rule.** A distance is `double.NaN` when either shape is `PlanarEmpty`.
- **Shared engine.** Both classes implement `IMovingObject<TShape>` (an `IObservable<TShape>` with a `Location`) and share one internal engine, so the push queue, the drainer and every index backend behave the same. The grid backend of an `RxGeometry` covers `RxSpatialOptions.PlanarGrid` instead of the latitude/longitude `Grid` (see Configuration).
- **No mixing.** An `RxGeometry` cannot query an `RxGeography`, or the reverse: the two classes have no common base type, so mixing them is a compile error by design.
- **Naming.** `RxGeographyDataStruct` keeps its historic name and selects the index for both classes.

## Shapes: `RxSpatial.Geo`

`RxSpatial.Geo` is the shape library under the core: a clean-room, zero-dependency replacement for the parts of SQL Server's spatial types that RxSpatial uses. It runs wherever .NET 10 runs. Every type is in the `RxSpatial.Geo` namespace.

| Geodetic (WGS84, metres) | Planar (Euclidean, your units) | Notes |
| --- | --- | --- |
| `GeoPoint(latitude, longitude)` | `PlanarPoint(x, y)` | A coordinate. `GeoPoint` validates its range and takes latitude first. |
| `GeoPointShape` | `PlanarPointShape` | A position, for example a GPS fix. |
| `GeoCircle(center, radiusMetres)` | `PlanarCircle(center, radius)` | An **exact** circle, not a polygon: a geofence, a reach, a buffered position. |
| `GeoPolygon.Rectangle(south, west, north, east)`, `.FromVertices(...)`, `.FromWkt("POLYGON ((lon lat, ...))")` | `PlanarPolygon.Rectangle(minX, minY, maxX, maxY)`, `.FromVertices(...)`, `.FromWkt(...)` | A **convex** polygon. Either winding order is accepted and stored counter-clockwise; `ToString()` gives WKT. |
| `GeoEmpty.Instance` | `PlanarEmpty.Instance` | The empty shape (an intersection that does not overlap). |

`GeoOperations` and `PlanarOperations` hold the maths: `Intersects`, `Intersection`, `Distance`, `Area`, `Bounds` and `ToVertices` (an outline for drawing or export, 128 segments per circle by default, like SQL Server's buffer; an overload writes into a `Span` without allocating, for redrawing on every update). `Wgs84` exposes the geodesic itself (`Inverse`, `Distance`, `Direct`).

```csharp
using RxSpatial.Geo;

var needle = new GeoPoint(47.6205, -122.3493);
var fence = new GeoCircle(needle, radiusMetres: 500);
var downtown = GeoPolygon.Rectangle(south: 47.600, west: -122.370, north: 47.618, east: -122.330);

bool inside = GeoOperations.Intersects(fence, new GeoPointShape(47.6230, -122.3480));   // true
double metres = GeoOperations.Distance(fence, downtown);                                 // 0 if they touch
GeoShape overlap = GeoOperations.Intersection(fence, downtown);                          // a convex GeoPolygon, the fence, or GeoEmpty
double squareMetres = GeoOperations.Area(overlap);                                       // the exact curved area
GeoPoint[] outline = GeoOperations.ToVertices(fence);                                    // 128 points on the circle
```

How it computes, and its limits:

- **Geodesics** are Vincenty's inverse and direct solutions on the WGS84 ellipsoid, converged to 1e-14 within 200 iterations. A nearly antipodal pair that does not converge gives `double.NaN`, never a wrong distance. A fast flat-earth estimate (the ellipsoid's radii of curvature at the mean latitude) with a 0.5 % guard band answers the clear cases of a circle test without the full solution.
- **Circles are exact.** Point-in-circle and the distances to a circle use the geodesic. The intersection of two circles is the analytic lens, returned as a `GeoPolygon` whose arcs are vertices at most 2π/128 apart; its `Area` is the exact area of the curved lens, not of the vertices.
- **Polygons are convex only.** A concave or self-intersecting ring throws `ArgumentException`. Point-in-polygon and the distances to a polygon are exact at any size. Polygon–polygon overlap, and the clipping inside `Intersection`, run in a local azimuthal-equidistant plane (`LocalPlane`).
- **Shapes are at most 500 km across** (`GeoShape.MaxExtent`; a circle's radius is at most 250 km), because the local plane is accurate only at that scale.
- **The antimeridian is not split.** `Bounds` is in degrees and does not wrap at ±180°, as before with SQL Server.
- **Hot paths do not allocate**: `Intersects`, `Distance`, `Area` and `Bounds` are 0 B.

### Breaking changes for `RxGeography` and `RxGeometry`

The core no longer depends on Microsoft.SqlServer.Types:

| Before | After |
| --- | --- |
| `RxGeography : IObserver<SqlGeography>, IObservable<SqlGeography>` | `RxGeography : IObserver<GeoShape>, IObservable<GeoShape>` |
| `RxGeometry : IObserver<SqlGeometry>, IObservable<SqlGeometry>` | `RxGeometry : IObserver<PlanarShape>, IObservable<PlanarShape>` |
| `Location` is a `SqlGeography?` / `SqlGeometry?` | `Location` is a `GeoShape?` / `PlanarShape?` |
| `RxIntersect`, `RxIntersection`, `RxDistance` and the `RxGeographyObservable` / `RxGeometryObservable` extensions take `SqlGeography` / `SqlGeometry` | They take `GeoShape` / `PlanarShape`; the member names and parameters are unchanged |
| `RxIntersection` yields whatever `STIntersection` returns | It yields `GeoEmpty`, a point (touching shapes), the contained shape, or a convex polygon |
| Distance NaN for different SRIDs or an empty geography | NaN for an empty shape or a non-converging (nearly antipodal) geodesic; there are no SRIDs |
| An empty shape pushed to the grid or tree backends throws `SqlNullValueException` | It throws `InvalidOperationException` (an empty shape has no bounds); the List backend still accepts it |
| `HighResolutionDateTime` (a Windows P/Invoke clock) | Removed; latency is timed with `Stopwatch` |

Translating shapes: `SqlGeography.Point(lat, lon, 4326)` becomes `new GeoPointShape(lat, lon)`; `point.STBuffer(r)` becomes `new GeoCircle(new GeoPoint(lat, lon), r)`; `SqlGeography.STPolyFromText(wkt, 4326)` becomes `GeoPolygon.FromWkt(wkt)` (if the polygon is convex); `SqlGeometry.Point(x, y, 0)` becomes `new PlanarPointShape(x, y)`. `STIntersects`, `STDistance`, `STIntersection` and `STArea` become `GeoOperations.Intersects`, `Distance`, `Intersection` and `Area`.

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

| Quantity | Budget | Worst measured |
| --- | --- | --- |
| Point–point distance | max(0.01 %, 1 cm) | 0.26 mm (30 cases) |
| Point–circle distance | 0.5 m | 0.14 m (103 cases) ¹ |
| Point–polygon distance | 0.5 m | 0.75 mm |
| Circle–circle / circle–polygon distance | 0.5 m plus the chord sag of each circle ¹ | 1.09 m / 0.51 m, both within budget |
| Areas (circle, polygon, lens, clipped circle) | 0.15 % | 0.13 % (a 3 km lens); circles 0.04 %, polygons 0.0001 % |
| Thin lenses (< 1 % of the disc) | 0.15 % of the analytic area | 1e-10 |
| Intersects, more than 0.5 m (plus sag) from a boundary | must agree | agrees in every case |

¹ SQL Server's `STBuffer` disc is an inscribed 128-gon, whose chords sag r(1 − cos(π/128)) ≈ 0.03 % of the radius inside the true circle (0.15 m for a 500 m fence, 0.6 m for a 2 km disc). The differences above are mostly that sag: RxSpatial.Geo measures to the exact circle. The planar maths matches SQL Server exactly (within 1e-13) wherever SQL Server is exact, that is, without circles.

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
    },
    "PlanarGrid": {
      "XCells": 10,
      "YCells": 10,
      "CoverageRegion": { "MinX": 0, "MinY": 0, "MaxX": 1000, "MaxY": 1000 }
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
| `Grid`                    | Grid backend dimensions and coverage rectangle for `RxGeography` (latitude/longitude).                                                                                                   | `10×10` over `46,-124 .. 49,-120` |
| `PlanarGrid`              | Grid backend dimensions and coverage rectangle for `RxGeometry` (X/Y, in the geometries' coordinate units).                                                                              | `10×10` over `0,0 .. 1000,1000`   |


## Modernisation notes (2026)

A summary of every change since the 2016 code is in [CHANGELOG.md](CHANGELOG.md). The profiling-driven performance and memory improvements (spatial index, `RxSpatial.Geo`, and the WPF demo's CPU and memory passes) are recorded with their before/after measurements in [docs/performance.md](docs/performance.md).

The codebase was originally built in 2014-2016 against .NET Framework 4.0/4.5 and no longer compiled from a fresh clone (machine-specific assembly hint paths, missing binaries, TFS bindings). It has been modernised in two passes — first a faithful migration, then a restructure to current standards:

- **.NET 10, C# 14 and SDK-style projects** throughout, arranged one folder per project under `solutions/` (with `docs` at the root) and a single `RxSpatial.slnx` that groups them into `src` / `samples` / `benchmarks` / `tests` solution folders, shared `Directory.Build.props` (nullable, implicit usings, artifacts output, warnings-as-errors in Release), central package management via `Directory.Packages.props`, `.editorconfig`, pinned `global.json`, and the standard endjin ZeroFailed build (`build.ps1`) with Dependabot automation on GitHub Actions (see [Build and CI](#build-and-ci)).
- **System.Reactive 7.0.0.** Rx upgraded from the 2.2.5 `Rx-*` package family; the `RxGeographyObservable` C# 14 extension members make the library's streams first-class Rx sequences (see above).
- **Microsoft.SqlServer.Types 170.1000.7.** The SQL Server 2025 generation of the official package supports modern .NET (netstandard2.0 + native win-x64/x86/arm64 runtimes), so the `SqlGeography` public API of the library was preserved at first (distance results aside — see below). It is now confined to the Windows-only `RxSpatial.SqlServer` package (see the cross-platform note below).
- **Tests and benchmarks.** The original `LoadTestApplication` and `memoryTestApplication` consoles (and the 2016 experiment outputs they had written into their source folders) were replaced by the `RxSpatial.Benchmarks` BenchmarkDotNet suite; behavioural coverage lives in `solutions/RxSpatial.Tests` (MSTest.Sdk). The originals remain in the git history.
- **Modern configuration.** The legacy `System.Configuration.ConfigurationManager` appSettings were replaced by typed `RxSpatialOptions` with constructor injection plus an ambient default bound from Microsoft.Extensions.Configuration (see Configuration above); the samples now use `appsettings.json` instead of `App.config`. The core library itself has no configuration package dependency at all.
- **Core library refactor.** File-scoped namespaces, nullable annotations that preserve the original null-flow, dead code removed, and a handful of verified bug fixes: the grid backend's non-square-grid maths (`longStep` and the cell-key stride), the grid's dead point-handling branch (`SqlString`-vs-`string` comparison that could never be true), and tree-backend unsubscription (`Remove`/`Contains` previously consulted the wrong index map, so disposing a subscription never detached observers from an R-tree backend).
- **Modern spatial indexes.** The 20-year-old vendored Java ports of the R-tree and RUM-tree (4,068 lines, LGPL 2.1, log4net-era) were replaced by `solutions/RxSpatial.Index`: a clean-room R*-tree engine shared by both trees, with the RUM-tree implemented per the paper (long stamps, per-item update memo, tombstones, a round-robin cleaning token over a leaf ring). The item-keyed API (`Insert`/`Update`/`Remove`/`Search`) made the core library's per-tree rectangle caches unnecessary, `Count` now reports live items, removing an unknown item returns `false` instead of throwing, and the whole solution builds warning-free with no analyzer exemptions. Correctness is guarded by seeded randomized oracle tests (hundreds of operations compared per-op against a naive scan). Steady-state update cost dropped from 229 µs to ~0.6 µs on the RUM-tree (with ~1000× less allocation); the LGPL obligation, log4net and the last `ConfigurationManager` usage left the repo with the ports (git history preserves them).
- **MapLibre instead of Bing Maps.** The retired Bing Maps WPF control (and with it the repo's last .NET Framework-era package) was replaced by MapLibre GL JS hosted in WebView2: `wwwroot/map.html` renders OpenStreetMap raster tiles (no API key), and the `MapLibreMap` class bridges C# and the map over JSON web messages (markers, lines, areas and drawing mode in; drawn areas with geographic coordinates out — which also replaced the old `ViewportPointToLocation` conversion). Because WebView2 is HWND-hosted and always paints above WPF content, the control panel now sits beside the map rather than overlaying it. Note MapLibre zoom levels use the 512px-tile convention — one level lower than the equivalent Bing value.
- **Idiomatic C# 14 review.** A full sweep for the library's Java heritage: `IContainer`/`GeoList`/`Grid`/`Tree` became `ISpatialBackend`/`ListBackend`/`GridBackend`/`TreeBackend`, `dump()` became `ToString()`, `AvgCalc` became `RunningAverage`, and the twelve continuous-query observers were rebuilt on primary-constructor bases (`RxObserverBase`/`RxRxObserverBase`, `VehicleObserverBase<TValue>` in the demo) that supply the `IObserver<T>` boilerplate so each query is a single expression. Public members were renamed to .NET conventions (`loc` → `Location`, `latency` → `AverageLatency`, `checkQueuedPushNum` → `QueuedPushCount`, `RxSpatialOverLoadException` → `RxSpatialOverloadException`), and the buffered drainer now goes to the thread pool instead of constructing an OS thread per push burst (~2× faster on the push-wait-push pattern a moving object produces). Reviewing the discarded `IDisposable` in the backends' `Subscribe` overloads turned up two further defects, both fixed and covered by tests: every spatial backend re-registered a disposed observer when the object it tracked next moved, and the grid could not unsubscribe a region observer (a geofence) at all. The build enforces the standard — `AnalysisMode=Recommended` plus the rules covering defect classes this codebase has actually shipped (culture-dependent formatting, discarded disposables, unqualified P/Invoke search paths), with the handful of deliberate exemptions recorded in `.editorconfig` alongside the reason.
- **Distances as `double` (breaking change).** The 2016 code returned distances as `SqlDouble`, so every consumer had to cast or call `.Value`. The distance queries now take `IObserver<double>` and the `DistanceTo` extensions return `IObservable<double>`, matching the `RxDistance(SqlGeography, IObserver<double>)` / `RxRxDistance(RxGeography, IObserver<double>)` signatures in the ICDE 2017, DEBS 2016 and SIGMOD 2016 papers. Where `STDistance` returns `SqlDouble.Null` (different SRIDs, an empty geography) the query emits `double.NaN`, which keeps one notification per location update; the map demo shows NaN as out of range.
- **`RxGeometry`, the planar moving object.** The ICDE 2017 paper names `RxGeometry` beside `RxGeography` but the authors never released it. The push queue, drainer and backend factory moved out of `RxGeography` into one internal engine, `MovingObjectCore<TShape, TOps>`, with the shape-specific operations in stateless structs whose static members the JIT calls directly; `RxGeography` (public members unchanged) and the new `RxGeometry` are thin facades over it. The grid and tree backends now index every shape by the same bounding rectangle, which made the grid cheaper for region pushes and fixed a grid defect where a region push notified a region observer once per shared cell. The public `ISpatialBackend` and `Unsubscriber` types gained a shape type parameter (`ISpatialBackend<TShape, T>`, `Unsubscriber<TShape>`).
- **Cross-platform core (breaking change).** The core no longer needs Windows or Microsoft.SqlServer.Types. The new `solutions/RxSpatial.Geo` is a clean-room shape library — Vincenty geodesics on WGS84, exact circles with an analytic lens, convex polygons, and planar twins — and `RxGeography` / `RxGeometry` now take `GeoShape` / `PlanarShape` (see [Breaking changes](#breaking-changes-for-rxgeography-and-rxgeometry)). Before the switch, SQL Server's own results for 201 Seattle-scale cases were recorded to `solutions/RxSpatial.Tests/Golden`, and the new maths is tested against them on every platform, against GeographicLib as an independent oracle, and with randomized property tests. The previous facades live on as `RxSqlGeography` / `RxSqlGeometry` in the Windows-only `RxSpatial.SqlServer` package. The latency clock moved from a Windows P/Invoke to `Stopwatch`, the samples and the experiments harness moved to the Geo types (the WPF demo keeps every tab's behaviour), and CI now builds and tests on Ubuntu, Windows and macOS.
- **Map demo performance pass.** Profiling the WPF demo (a UI-automation harness with `dotnet-counters`, `dotnet-trace`, `dotnet-gcdump` and the page's DevTools metrics) showed that the time went to the WebView2 renderer and GPU processes, not to the continuous queries. Three changes followed. `MapLibreMap` now posts one web message per replay tick instead of one per command (25–70), merges updates to the same marker, line or overlay, sends only the marker fields that changed, and writes outlines as integer micro-degree steps straight to UTF-8: on *Overlap* with all pairs watched and a zone, 87 KB in 72 messages became 35 KB in one, and managed allocation fell from about 490 to 160 KB per tick. `map.html` caps MapLibre's data-driven repaints at 60 a second (it had redrawn the map about 220 times a second on a 240 Hz display), which cut the renderer process from 104% to 42% of a core and the GPU process from 131% to 37% on that tab. The replay keeps its own clock, so 25 and 50 updates/s are now achieved (21 and 32 before). The demo publishes a `MapRunRxSpatial` meter (tick and send time, pushes, web messages and characters) for `dotnet-counters`.
- **Map demo memory pass.** A memory breakdown (VMMap, `dotnet-dump` `eeheap`, `dotnet-gcdump`, GPU process memory counters and the page's DevTools metrics) showed that the managed heap was never the problem: about 7 MB live, of which the 20 trajectories are 0.6 MB. The memory was native. WPF rendered its window, a control panel next to a WebView2 child window, through a Direct3D 9 device and a swap chain the size of the screen. The panel is now rendered in software, which takes the app's private bytes from 171–180 MB to about 85 MB on *Overlap* and from 135 to 79 MB at idle. In the map page, V8 had grown its young generation to hold the GeoJSON garbage of every frame. `--js-flags=--max-semi-space-size=2` halves the renderer process on *Overlap* and *Convoy link* (about 200 MB to 105 MB) at the same renderer CPU. Dynamic PGO is off (about 4 MB of code and loader heap). While the window is minimised, the map is hidden and WebView2's memory target is low: the WebView2 processes' working sets fall from about 610 to 100 MB, and the page stops drawing a map that nobody can see. `GeoOperations.Intersection` now builds its rings in per-thread scratch lists, with bit-identical results, which cut managed allocation on *Overlap* by 28%.

## Licence

RxSpatial is released under the [MIT License](LICENSE), the licence that the original authors added to [williamshyy/RxSpatial](https://github.com/williamshyy/RxSpatial) in October 2026. The contributors are listed in [AUTHORS.txt](AUTHORS.txt). The NuGet packages that RxSpatial references keep their own licences (see the Microsoft.SqlServer.Types note under [SQL Server types](#sql-server-types-rxspatialsqlserver)). The upstream repository also contains R-tree and RUM-tree ports under the GNU LGPL v2.1. RxSpatial does not use them: `RxSpatial.Index` is a clean-room replacement, and the ports remain only in this repository's git history.
