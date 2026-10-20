---
paths:
  - "src/RxSpatial.Geo/**"
  - "src/RxSpatial/*Operations.cs"
  - "src/RxSpatial.SqlServer/**"
  - "tests/RxSpatial.Tests/Geo/**"
  - "tests/RxSpatial.Tests/Golden/**"
  - "tests/RxSpatial.SqlServer.Tests/Golden/**"
  - "tests/golden/**"
---

# Shape maths (`RxSpatial.Geo`) and SQL Server fidelity

- Zero dependencies, public, clean-room. Closed hierarchies: `GeoPointShape`/`GeoCircle`
  (exact, radius ≤ 250 km)/`GeoPolygon` (convex only, CCW)/`GeoEmpty`, and the planar twin.
  Concave or self-intersecting rings and shapes over 500 km (`MaxExtent`) are rejected;
  concave fences need `RxSqlGeography`.
- `Wgs84` is Vincenty (≤200 iterations, NaN on non-convergence — test input: the Space Needle's
  antipode −47.6205, 57.6507). `Bounds` is degree-based and does not split at ±180°.
- Point-in-polygon and distances to polygons are exact; polygon–polygon overlap and clipping
  are local-plane approximations (documented). Circle∩circle is an analytic lens (`Area` exact).
- Hot paths must allocate 0 B (`AllocationTests`). Bounds and polygon areas are computed at
  construction, so their benchmarks read a field. `Intersection` uses per-thread scratch lists.
- Avoid 32-byte struct copies on hot paths (`GeoBounds`, `ReducedPoint`; read bounds by
  `ref readonly`): a dirty 256-bit register makes the following native `Math.SinCos` pay an
  AVX–SSE transition penalty (~270 ns). Diagnose with `DOTNET_PreferredVectorBitWidth=128`.
- Optimisations must be bit-identical, or justified against the fidelity budget. Prove it with
  a differential run: print every public operation's raw result bits over seeded random pairs,
  build against a `git worktree` of the old commit and against HEAD, then `cmp`.
- `InternalsVisibleTo` names the `RxSpatial.Tests` assembly — keep that assembly name.
- Golden values: `tests/golden/{geodetic,planar}.json` were recorded from SQL Server by
  `GoldenValueRecorder` (RxSpatial.SqlServer.Tests, only with `RXSPATIAL_RECORD_GOLDEN=1`).
  `GoldenFidelityTests` (any OS) check Geo within budget: point–point max(0.01 %, 1 cm);
  point–circle/polygon 0.5 m (+ SQL's 128-gon chord sag for circles); areas 0.15 %; intersects
  agree > 0.5 m from a boundary. `GoldenValueGuardTests` (Windows) check live SQL Server still
  matches the JSON. GeographicLib.NET is a test-only oracle — never a runtime dependency.
- RxSql* facades: Null `STDistance` (different SRIDs, empty geography) → NaN in
  `SpatialDistance.ToDouble`; `POINT EMPTY` throws on tree/grid backends. Microsoft.SqlServer.Types
  170.x ships under pre-release (CTP) licence terms — keep it confined to the SqlServer projects.
