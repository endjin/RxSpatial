---
paths:
  - "solutions/RxSpatial.Benchmarks/**"
  - "solutions/archive/RxSpatial.SqlServer.Benchmarks/**"
  - "solutions/RxSpatial.Experiments/**"
  - "docs/performance.md"
---

# Benchmarks, experiments and measurement

- This workstation's absolutes swing ~2× with load: compare within-run ratios. For before/after,
  build the baseline in a `git worktree` and run baseline → candidate → baseline in one session.
- Never edit the library projects under `solutions/` while a BenchmarkDotNet run is live (it rebuilds from source per benchmark),
  and never build while the experiments harness runs (locked DLLs, wall-clock timing).
- Windows power throttling (EcoQoS) slows a process started from a non-foreground console
  (agent or CI shell) ~6.7× a few seconds into sustained load on this hybrid i7-13700H. Every
  hand-rolled timing loop or harness must call `PowerThrottling.OptOut()`
  (`solutions/RxSpatial.Benchmarks/PowerThrottling.cs`); `RXSPATIAL_ALLOW_POWER_THROTTLING=1` restores the OS
  default. Tell-tale: a single-thread rate that decays across consecutive cells. BenchmarkDotNet
  children were not throttled.
- Scaling tables: dedicated threads, padded shared counter and result slots, a busy-cores column
  and a repeated 1-thread row at the end to expose drift.
- Coverage runs: `--inProcess --invocationCount 512 --unrollFactor 1` (a Dry job undercounts loops).
- Experiments harness (`solutions/RxSpatial.Experiments`): "SqlSpatial" = the List backend (the
  paper's own wording). On Geo every variant is sub-ms, so the paper's List-vs-tree gaps sit in
  thread-pool dispatch noise; the SQL-backed reproduction is recorded in `docs/icde-experiments.md`. Pre-fix
  multi-object figures (including the "~4× STIntersects ceiling") ran throttled — re-run before
  relying on them. Max-load probes must run well past `QueueLimit` rounds and fail on mean
  latency > interval.
- `RxSpatial.SqlServer.Benchmarks` (Windows) holds SQL baselines and `SqlVsGeoOperationsBenchmarks`;
  keep `RxSpatial.Benchmarks` cross-platform.
