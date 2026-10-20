# Performance and memory improvements

This page records the profiling-driven improvements made while modernising RxSpatial (July–October 2026): what was measured, what changed, and the before/after numbers. The README and CLAUDE.md give the short version; this page keeps the evidence in one place.

**Caveats for every table:**
- All figures come from one workstation: an i7-13700H (6 performance cores with hyper-threading plus 8 efficiency cores), Windows 11, a 4K 240 Hz display, the Balanced power plan, on AC power.
- Absolute timings on this machine swing about 2× with load. Each before/after pair below was measured in the same session, back to back. Compare ratios, not absolutes across tables.
- Windows 11 power throttling (EcoQoS) slows a process it treats as background after about 4 s of sustained load. See [Measuring on this machine](#measuring-on-this-machine).

## 1. Spatial index: clean-room R\*-tree and RUM-tree (`RxSpatial.Index`)

The vendored LGPL Java-port trees were replaced by a clean-room engine (commits `9c5deab`..`9c64402`). The engine was then tuned with `dotnet-trace`/`dotnet-dump`:
- a containment fast path in ChooseSubtree;
- ArrayPool scratch for reinsertion (`4e38362`);
- a bottom-up in-place update for local moves (`3f29809`).

Measured at N = 1000 with BenchmarkDotNet, plus a 90 s soak:

| Metric                       | Vendored port         | Clean-room rewrite | + profile-guided (`4e38362`) | + in-place update (`3f29809`) |
|------------------------------|-----------------------|--------------------|------------------------------|-------------------------------|
| RUM-tree update              | 229,330 ns / 18,133 B | 2,784 ns / 27 B    | **609 ns / 17 B (377×)**     | unchanged                     |
| R\*-tree update (teleport)   | 602 ns / 447 B        | 1,237 ns / 19 B    | **1,009 ns / 11 B**          | unchanged                     |
| R\*-tree update (local move) | —                     | 0.90× of teleport  | —                            | **0.42× of teleport, 1 B**    |
| Bulk insert (R\* / RUM)      | —                     | 2.31 / 4.37 ms     | **1.92 / 2.60 ms**           | unchanged                     |
| 90 s mixed soak              | —                     | 17.34 M ops        | **71.86 M ops (4.14×)**      | —                             |

The checksum per operation in the soak was identical to six digits, confirming the search semantics did not change.

## 2. Buffered push path (`871ca57`)

The drainer for buffered location pushes is now dispatched to the thread pool instead of `new Thread(...)` per burst. A push-wait-push cycle went from 438/442/411 µs to 182/165/247 µs (three runs each), about 2× faster.

## 3. Cross-platform shape maths: `RxSpatial.Geo` vs SQL Server (`ace954a`..`733fc91`)

The core moved from `Microsoft.SqlServer.Types` to the clean-room, zero-dependency `RxSpatial.Geo`. It uses Vincenty geodesics, exact circles and convex clipping. Accuracy against SQL Server:
- point–point distance within 0.26 mm;
- point–circle distance within 0.14 m;
- areas within 0.125 %;
- no Intersects disagreements.

The fidelity budget is in the README. Per operation (`SqlVsGeoOperationsBenchmarks`):

| Operation                | SQL Server  | Geo                           | Allocated (SQL / Geo) |
|--------------------------|-------------|-------------------------------|-----------------------|
| Bounds of a circle       | 18.4 µs     | ≈0 (precomputed)              | 29,360 B / 0 B        |
| Point in circle, inside  | 58.0 µs     | 20.2 ns                       | 336 B / 0 B           |
| Point in circle, outside | 58.4 µs     | 1.6 ns                        | 336 B / 0 B           |
| Disc–disc intersects     | 226 µs      | 23.3 ns                       | 336 B / 0 B           |
| Point–point distance     | **85.7 ns** | 152 ns                        | 0 B / 0 B             |
| Point–circle distance    | 123 µs      | 139 ns                        | 336 B / 0 B           |
| Lens (disc ∩ disc)       | 190 µs      | 21.0 µs                       | 4,304 B / 6,280 B     |
| Area of a lens           | 113 µs      | ≈0 (computed at construction) | 168 B / 0 B           |

Point–point distance is the one operation where SQL Server is faster.

End to end, one location push through each backend (`SpatialBackendBenchmarks`):

| Backend, observers | Geo            | SQL Server         |
|--------------------|----------------|--------------------|
| List, 1000         | 6.63 µs / 40 B | 107.2 ms / 328 KB  |
| HashMap, 1000      | 115 ns / 40 B  | 1.395 ms / 32.8 KB |
| R\*-tree, 1000     | 71 ns / 84 B   | 77.7 µs / 28.9 KB  |
| RUM-tree, 1000     | 74 ns / 84 B   | 77.4 µs / 28.9 KB  |

## 4. `RxSpatial.Geo` performance pass (`5dd5b85`..`7df0542`)

New benchmarks covered the gaps:
- geodesic primitives;
- `LocalPlane`;
- shape construction and convexity validation;
- circle and lens outlines;
- polygon bounds;
- `GeoScalingBenchmarks` on 1–20 threads.

The optimisations then applied were:
- a cheaper longitude wrap;
- reading bounds by reference;
- hoisting the reduced latitude;
- shortlisting the polygon edges that can be nearest;
- planar circle–polygon Intersects;
- a guarded flat-earth point-in-polygon test;
- span `ToVertices` overloads.

Results are **bit-identical**: a 100,000-pair differential run showed no change, and the golden, oracle, property and allocation tests passed unchanged.

| Benchmark                   |   Before |                        After | Ratio |
|-----------------------------|---------:|-----------------------------:|------:|
| Circle–polygon intersects   | 1,036 ns |                       173 ns |  0.17 |
| Point in polygon, inside    |  54.0 ns |                      27.6 ns |  0.51 |
| Point in circle (guarded)   |  20.2 ns |                      13.9 ns |  0.69 |
| Disc–disc intersects        |  23.3 ns |                      17.3 ns |  0.74 |
| Circle–polygon distance     |   880 ns |                       558 ns |  0.63 |
| Point–polygon distance      |   887 ns |                       738 ns |  0.83 |
| Disc ∩ rectangle            | 10.28 µs |                      8.39 µs |  0.82 |
| Lens intersection           | 20.96 µs |                     18.64 µs |  0.89 |
| FastDistance                |  12.6 ns |                       9.9 ns |  0.79 |
| Circle outline, 64 segments | 10.57 µs | 9.03 µs (span overload: 0 B) |  0.85 |

**A pitfall found along the way.** Copying a shape's 32-byte bounds by value left the upper halves of the CPU's 256-bit vector registers dirty. The following native `Math.SinCos` calls then paid an AVX–SSE transition penalty of about 270 ns. Reading the bounds by `ref readonly` fixed it.

The memory pass (`acf3944`) later made `Intersection` build its rings in per-thread scratch lists, again with bit-identical results:

| Benchmark         | Before            | After             |
|-------------------|-------------------|-------------------|
| Lens intersection | 19.5 µs / 6,280 B | 18.8 µs / 4,144 B |
| Circle ∩ polygon  | 8.76 µs / 4,032 B | 8.38 µs / 1,744 B |
| Polygon ∩ polygon | 1.80 µs / 776 B   | 1.76 µs / 384 B   |

## 5. WPF demo: CPU pass (`e1895a4`..`d3f1131`)

Profiling used a UI Automation harness with `dotnet-counters`, `dotnet-trace`, `dotnet-gcdump`, per-process cycle counters and the page's DevTools metrics. The time went to the WebView2 renderer and GPU processes, not to the continuous queries: the UI thread was never more than 15 % busy.

Changes:
1. **A `MapRunRxSpatial` meter** (tick and send time, pushes, web messages and characters) for `dotnet-counters`. It costs nothing unless something listens.
2. **One web message per replay tick,** carrying only what changed. Updates to the same marker, line or overlay are merged, written straight to UTF-8, and outlines are sent as integer micro-degree steps.
3. **Data-driven map repaints capped at 60 a second.** MapLibre had redrawn about 220 times a second on the 240 Hz display. Panning and zooming keep the full rate.
4. **A replay clock that achieves the requested rate.** `DispatcherTimer` fired only on Windows' 15.6 ms tick, so 25 and 50 updates/s actually ran at 21 and 32.

Per tick, before → after:

| Scenario                        | UI ms       | Renderer ms | GPU ms      | Messages / chars     | Allocated KB | Rate achieved |
|---------------------------------|-------------|-------------|-------------|----------------------|--------------|---------------|
| Overlap, all pairs + zone, 25/s | 5.49 → 2.64 | 65.0 → 34.2 | 62.7 → 31.0 | 72 / 87K → 1 / 35K   | 492 → 165    | 19.5 → 25     |
| Convoy link, 50/s               | 1.47 → 0.92 | 39.5 → 17.1 | 37.9 → 16.0 | 32 / 4.9K → 1 / 1.9K | 20.7 → 8.4   | 31.2 → 50     |
| Near vehicle, all pairs, 50/s   | 1.12 → 0.86 | 6.8 → 3.5   | 8.5 → 4.3   | 54 / 7.0K → 1 / 1.4K | 22.2 → 7.0   | 31.4 → 50     |
| Geofence (RUM-tree), 50/s       | 0.77 → 0.78 | 5.0 → 3.1   | 4.4 → 2.7   | 26 / 3.9K → 1 / 1.4K | 15.8 → 8.6   | 31.2 → 50     |

The repaint cap on its own took the Overlap renderer from 104 % to 42 % of a core, and the GPU process from 131 % to 37 %. The baseline build also leaked page event listeners across tab switches (589 → 1,042 in a 5-minute soak); the new build does not.

## 6. WPF demo: memory pass (`acf3944`..`c3bb880`)

A memory breakdown (VMMap, `dotnet-dump` `eeheap`, `dotnet-gcdump`, GPU memory counters and the page's DevTools metrics) showed the managed heap was never the problem. It held about 7 MB live, of which the 20 trajectories are 0.6 MB. The memory was native.

Kept, with what each measured:

| Change                                                                            | Effect                                                                                                                                                   |
|-----------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------|
| WPF panel rendered in software (`RenderOptions.ProcessRenderMode = SoftwareOnly`) | App private −88 MB, −25 threads, app GPU memory −66 to −94 MB. WPF had held a Direct3D 9 swap chain the size of the 4K screen for a mostly static panel. |
| `--js-flags=--max-semi-space-size=2` for the map page                             | Renderer 204 → 106 MB on Overlap; renderer CPU 96 → 87 %. V8 had grown its young generation to hold each frame's GeoJSON garbage.                        |
| `TieredPGO` off for the demo                                                      | Code heap 3.7 → 1.8 MB, CLR total about −4 MB; tick and send times unchanged.                                                                            |
| Map hidden and WebView2 `MemoryUsageTargetLevel = Low` while minimised            | WebView2 working sets 613 → 101 MB; renderer CPU 42 → 7 %.                                                                                               |
| Geo `Intersection` per-thread scratch lists                                       | Overlap allocation −28 %; gen0 collections 35.5 → 27.3 a minute.                                                                                         |

Rejected, because each change made no measurable difference or cost more than it saved:
- `gcConcurrent=0` and `GCConserveMemory`;
- a smaller first GC generation (`GCgen0size`, which also tripled gen0 collections);
- `TieredCompilation=false`;
- a 1 MB semi-space;
- an in-process GPU;
- a smaller tile cache;
- compact trajectory structs.

Before → after, MB:

| Scenario                        | App working set | App private | Renderer  | WebView2 total | Page JS heap |
|---------------------------------|-----------------|-------------|-----------|----------------|--------------|
| Idle                            | 198 → 139       | 135 → 79    | 46 → 43   | 220 → 216      | 4.8 → 4.8    |
| Near vehicle, all pairs, 50/s   | 229 → 153       | 158 → 87    | 63 → 56   | 255 → 249      | 14 → 9       |
| Overlap, all pairs + zone, 50/s | 246 → 152       | 171 → 86    | 218 → 104 | 464 → 329      | 67 → 23      |
| Geofence (RUM-tree), 50/s       | 249 → 152       | 174 → 84    | 134 → 78  | 347 → 290      | 62 → 13      |
| Convoy link, 50/s               | 250 → 152       | 175 → 84    | 196 → 106 | 424 → 326      | 64 → 19      |

CPU did not regress: on Overlap at 50/s the UI thread went 11.1 → 9.2 %, the renderer 106 → 96 % and the GPU process 75 → 63 %. A 5-minute soak (45 tab switches) shows flat managed and JS heaps, DOM nodes and event listeners.

## Measuring on this machine

- **Power throttling.** Windows 11 throttles (EcoQoS) a process started from a non-foreground console after about 4 s of sustained load; throughput falls about 6×. The experiments harness and benchmarks opt out (`benchmarks/Shared/PowerThrottling.cs`; set `RXSPATIAL_ALLOW_POWER_THROTTLING=1` to keep the old behaviour). With the opt-out, `RxSpatial.Geo` scales 7.9× on 20 threads; throttled, the harness had read about 1×. BenchmarkDotNet's child processes were unaffected. Experiment results recorded before this fix ran throttled, including the SQL Server "~4× scaling ceiling", and should be re-measured before anyone relies on them.
- **Display refresh rate** changes WebView2 renderer and GPU numbers, by up to about 3× between sessions on the 240 Hz display. Compare WPF demo runs back to back only.
- **UI-thread CPU** must be measured with cycle counters: `TotalProcessorTime` is charged in whole 15.6 ms ticks.
- **Never take a full `dotnet-dump` before a memory measurement.** It inflated the app's working set from about 200 to 600 MB in early runs.

The measurement harnesses (UI Automation scenarios, counters, page probes) lived in temporary folders during this work and are not in the repo. The backlog has an item to promote a cleaned-up UI check into `tools/`.
