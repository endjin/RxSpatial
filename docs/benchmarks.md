# Benchmarks

To run the benchmarks (BenchmarkDotNet requires a Release build):

```shell
dotnet run --project solutions/RxSpatial.Benchmarks -c Release -- --filter "*"
dotnet run --project solutions/archive/RxSpatial.SqlServer.Benchmarks -c Release -- --filter "*"   # Windows only, archived: the SQL Server baselines
```

`SpatialBackendBenchmarks` measures the cost of pushing one location update through `RxGeography` per index backend — the list backend scales linearly with the observer count while the grid and tree backends prune by zone. `PlanarBackendBenchmarks` runs the same workload through `RxGeometry` on a 1,000 × 1,000 floor. `solutions/archive/RxSpatial.SqlServer.Benchmarks` (Windows) runs both workloads through `RxSqlGeography` / `RxSqlGeometry` as the SQL Server baseline. In one short run at 1,000 observers (2 km zone discs, 50 m push discs), a push cost:

| Backend        | `RxGeography` (Geo) | `RxSqlGeography` (SQL Server) | Planar: `RxGeometry` / `RxSqlGeometry` |
|----------------|---------------------|-------------------------------|----------------------------------------|
| List           | 6.6 µs              | 107 ms                        | 2.8 µs / 1.4 ms                        |
| Grid (HashMap) | 115 ns              | 1.4 ms                        | 105 ns / 36 µs                         |
| R\*-tree       | 71 ns               | 78 µs                         | 79 ns / 11.6 µs                        |
| RUM-tree       | 74 ns               | 77 µs                         | 91 ns / 11.2 µs                        |

Allocation per push is 40–116 B on Geo, against 29–328 KB (geodetic) and 1.2–328 KB (planar) on SQL Server. The List ratio is extreme because Geo rejects most circle–circle pairs by their degree bounds before any geodesic, while SQL Server intersects two 128-gons every time.

`GeoOperationsBenchmarks` times each RxSpatial.Geo operation on its own, and `SqlVsGeoOperationsBenchmarks` (Windows) times the same operations on SQL Server as the baseline of each category. One short run (`--job short`, i7-13700H), on the 500 m Space Needle fence and two 2 km discs 1.5 km apart:

| Operation                        | SQL Server  | RxSpatial.Geo | Geo ÷ SQL | Allocated (SQL / Geo) |
|----------------------------------|-------------|---------------|-----------|-----------------------|
| Bounds of a circle ¹             | 18.4 µs     | 0.2 ns        | —         | 29,360 B / 0 B        |
| Point in circle, inside          | 58.0 µs     | 20.2 ns       | 0.0003    | 336 B / 0 B           |
| Point in circle, 100 m outside ² | 58.4 µs     | 1.6 ns        | 0.00003   | 336 B / 0 B           |
| Disc–disc intersects             | 226 µs      | 23.3 ns       | 0.0001    | 336 B / 0 B           |
| Point–point distance             | **85.7 ns** | 152 ns        | **1.77**  | 0 B / 0 B             |
| Point–circle distance            | 123 µs      | 139 ns        | 0.001     | 336 B / 0 B           |
| Lens (disc ∩ disc)               | 190 µs      | 21.0 µs       | 0.11      | 4,304 B / 6,280 B     |
| Area of the lens ¹               | 113 µs      | 0.4 ns        | —         | 168 B / 0 B           |

¹ A Geo shape computes its bounds (and a polygon its area) when it is constructed, so these rows read a field: the work moves to the constructor. SQL Server's bounds row is the WKB-envelope path that `RxSqlGeography`'s tree and grid backends take. ² Rejected by the bounding box before any geodesic. SQL Server stays faster for a single point–point distance (Geo iterates Vincenty's solution to 1e-14), and the lens allocates more on Geo, because it builds a polygon with its arc vertices.

The table above was recorded before the optimisation pass described next; the Geo column is now faster on every row it shares with that pass.

## RxSpatial.Geo optimisation pass (October 2026)

More benchmarks now cover the geodesic primitives (`GeodesyBenchmarks`: Vincenty inverse and direct, `FastDistance`, `LocalPlane.Project` / `Unproject`), polygon construction (`ShapeConstructionBenchmarks`: `Rectangle`, `FromVertices`, `FromWkt` at 4 and 64 vertices, and the convexity validation on its own), outlines (`OutlineBenchmarks`: `ToVertices` at 64 and 128 segments, which the WPF demo calls on every update for each reach circle) and multi-core throughput (`GeoScalingBenchmarks`, below). A profile of these runs led to four changes. Each change gives bit-identical results: a 100,000-pair differential run of every public operation matched the earlier build bit for bit, and the golden fidelity, GeographicLib oracle, property and allocation tests pass unchanged.

- **Longitude wrapping without `Math.IEEERemainder`.** The remainder returns its argument unchanged in [−180°, 180°], so that range now skips the helper call (about 3 ns of the 12.6 ns `FastDistance`).
- **Bounds read in place.** A copy of the 32-byte `GeoBounds` let the JIT use a 256-bit register. Its dirty upper half then caused an AVX–SSE transition penalty in the native `Math.SinCos` that `FastDistance` calls, and point-in-circle tests ran at ~295 ns instead of 20 ns. Shapes now expose their bounds by `ref readonly`.
- **Reduced latitude worked out once per start point** (`ReducedPoint`), when many geodesics share a start: projecting a ring, polygon construction, intersection results and circle outlines.
- **Fewer exact geodesics for polygons.** The distance to a polygon measures only the edges that can be nearest, which the polygon's own plane shortlists within a proven error bound. Circle–polygon `Intersects` decides in that plane when the plane distance is clear of the radius, instead of running `Contains` twice and measuring every edge. Point-in-polygon tries the flat-earth offset first, within a bound of twice its worst measured error, and runs the Vincenty projection only near an edge.

`GeoOperations.ToVertices` and `PlanarOperations.ToVertices` also gained overloads that write into a `Span` and allocate nothing. Same machine, same session, default BenchmarkDotNet job, the baseline run before and after the optimised one (the before column is their mean; no row drifted by more than 1% between them except the noisy sub-100 ns backend rows):

| Benchmark                              |             Before |                                            After | After ÷ before |
|----------------------------------------|-------------------:|-------------------------------------------------:|---------------:|
| Point in circle, inside (guarded)      |            20.2 ns |                                          13.9 ns |           0.69 |
| Point in polygon, inside               |            54.0 ns |                                          27.6 ns |           0.51 |
| Disc–disc intersects                   |            23.3 ns |                                          17.3 ns |           0.74 |
| Circle–polygon intersects              |           1,036 ns |                                           173 ns |       **0.17** |
| Circle–polygon distance                |             880 ns |                                           558 ns |           0.63 |
| Point–polygon distance                 |             887 ns |                                           738 ns |           0.83 |
| Lens (disc ∩ disc)                     |           20.96 µs |                                         18.64 µs |           0.89 |
| Disc ∩ rectangle                       |           10.28 µs |                                          8.39 µs |           0.82 |
| `FastDistance`                         |            12.6 ns |                                           9.9 ns |           0.79 |
| `Wgs84.Direct`                         |             168 ns |                                           155 ns |           0.93 |
| `ToVertices`, 2 km circle, 64 segments | 10.57 µs / 1,048 B | 9.03 µs / 1,048 B (span overload: 8.96 µs / 0 B) |           0.85 |
| `GeoPolygon.FromVertices`, 64 vertices |           15.86 µs |                                         14.60 µs |           0.92 |
| Mixed Distance/Intersects, 1 thread    |             290 ns |                                           248 ns |           0.85 |

Vincenty's inverse itself (131 ns at 1.4 km, 176 ns at 230 km) and the backend push rows did not change beyond noise: a push is dominated by the index and the observers, not by the maths. Two rows read slower, and neither is a regression in the maths. The `Bounds` rows went from "indistinguishable from an empty method" to 0.2 ns, but a re-check with three launches of each build gave the reverse (0.24 ns before, 0.003 ns after), so this is BenchmarkDotNet's zero-measurement floor. `PlanarPolygon.FromVertices` with 4 vertices read 88 → 94 ns, and 88.7 → 92.7 ns (+4%, within 2σ) in the re-check, although its code path did not change: this is code layout, not work.

**Multi-core scaling.** `GeoScalingBenchmarks` runs the shape maths alone on 1–20 dedicated threads. Each thread pulls one route point at a time from a padded shared counter and tests it against shared fences. Results are kept in locals and published once, into padded slots. On the i7-13700H (6 performance cores with hyper-threading and 8 efficiency cores, 20 logical processors):

| Threads | 2,000-fence `Intersects` (Mops/s, speed-up) | Mixed Distance/Intersects (Mops/s, speed-up) |
|--------:|--------------------------------------------:|---------------------------------------------:|
|       1 |                                 205 (1.00×) |                                  4.0 (1.00×) |
|       2 |                                 436 (2.12×) |                                  8.0 (1.99×) |
|       4 |                                 730 (3.56×) |                                 14.6 (3.63×) |
|       8 |                                 966 (4.71×) |                                 21.7 (5.38×) |
|      14 |                               1,079 (5.26×) |                                 28.9 (7.17×) |
|      20 |                               1,191 (5.81×) |                                 33.8 (8.37×) |

The maths scales. The compute-heavy mix reaches 8.4× at 20 threads. The cheap fence loop (about 5 ns an operation, mostly the bounding-box test) reaches 5.8×, because it is limited by memory more than by arithmetic. Past 6 threads the extra threads are hyper-threads and efficiency cores, so the speed-up is sub-linear on this hardware.

`TreeIndexBenchmarks` compares the R*-tree (the per-category baseline) against the RUM-tree for bulk insertion, the frequent-update workload the RUM-tree was designed for, and intersection queries (including the allocation-free buffered `Search` overload). Because both trees share the same R* engine, the measured delta is purely the update-memo strategy. After profile-guided optimisation of the engine (a containment fast path in ChooseSubtree found via dotnet-trace, and pooled reinsertion scratch found via dotnet-dump heap analysis), the paper's thesis holds in memory too: RUM-tree updates run at roughly 0.6× the cost of an R* remove-and-reinsert (~0.6 µs vs ~1.0 µs at N=1000, and ~375× the original vendored port's 229 µs) with near-zero steady-state allocation. Coverage-driven `Churn` (delete + reinsert) and `UpdateLocal` (small-delta tracking moves) categories then motivated a bottom-up fast path: when a moved entry still fits its leaf's recorded cover it is rewritten in place, dropping local moves to ~0.42× the cost of a teleporting update with ~zero allocation — so the R*-tree now wins tracking workloads while the RUM-tree wins teleport-heavy ones.

