---
paths:
  - "solutions/src/RxSpatial.Index/**"
  - "solutions/src/RxSpatial/Backends/**"
  - "solutions/tests/RxSpatial.Tests/*Index*"
  - "solutions/tests/RxSpatial.Tests/*Churn*"
  - "solutions/tests/RxSpatial.Tests/*RumTree*"
---

# Spatial index (`RxSpatial.Index`) and backends

- One shared engine, `RStarCore` (struct-generic leaf filters), under two facades: `RStarTree`
  (item-keyed, physical updates, bottom-up in-place fast path for local moves) and `RumTree`
  (update memo, lazy deletion, round-robin cleaning token over a circular leaf ring).
- Correctness anchor: `SpatialIndexOracleTests` (seeded randomized workloads vs a naive-scan
  oracle). Keep it green through any engine change.
- RUM-tree stability law: steady pending garbage ≈ CleaningInterval × leaf count — keep the
  interval well below average leaf fill.
- Residual uncovered `RStarCore` lines are defensive-by-invariant (single-chain condense, roots
  keep ≥2 children, the cleaning ring never empties) — don't chase them with tests.
- The grid indexes by `Rect` via `GridLayout`; a multi-cell push reports each observer once
  (from the first cell the two blocks share), not once per shared cell.
- Performance changes: compare same-session ratios (`TreeIndexBenchmarks`) and record results
  in docs/performance.md §1.
