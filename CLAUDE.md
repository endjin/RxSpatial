# CLAUDE.md

<!-- Kept under ~150 lines on purpose (Anthropic guidance: <200, every line loads each session).
     Topic detail lives in path-scoped rules under .claude/rules/ that load only when matching
     files are touched. History is in CHANGELOG.md; measurements in docs/performance.md. -->

RxSpatial: the SIGMOD/DEBS 2016 / ICDE 2017 research library for reactive spatio-temporal
queries. `RxGeography` (geodetic) and `RxGeometry` (planar) moving objects push locations;
continuous distance/intersect/intersection queries notify observers. .NET 10 / C# 14 /
System.Reactive 7. The core is cross-platform; only the WPF demo and the archived `RxSpatial.SqlServer` projects are
Windows-only. House .NET standards: `.endjin/llm-kb/dotnet` (gitignored) — consult before .NET work.

## Commands

```shell
dotnet build solutions/RxSpatial.slnx -c Release --no-incremental   # Windows only (slnx has Windows-only projects); must be 0 warnings
dotnet test --solution solutions/RxSpatial.slnx -c Release   # all tests (~287, RxSpatial.Tests; the archived SQL Server projects are not built)
dotnet run --project solutions/RxSpatial.Examples -c Release   # end-to-end smoke test: must print "All scenarios passed."
./build.ps1                                               # the CI build (ZeroFailed, pwsh 7): build, test + coverage, smoke, SBOM
./build.ps1 -Tasks Build,Test                             # quicker local CI check

# Cross-platform projects only (what Linux/macOS CI builds)
dotnet build solutions/RxSpatial.CrossPlatform.slnf -c Release
dotnet test --project solutions/RxSpatial.Tests/RxSpatial.Tests.csproj -c Release

# Linux check of the committed HEAD (Docker Desktop, Linux engine, from bash)
git archive HEAD | docker run --rm -i mcr.microsoft.com/dotnet/sdk:10.0 bash -c \
  'mkdir /w && tar -xf - -C /w && cd /w && dotnet test --project solutions/RxSpatial.Tests/RxSpatial.Tests.csproj -c Release && dotnet run --project solutions/RxSpatial.Examples -c Release'

# Benchmarks / experiments (long-running; see .claude/rules/benchmarks.md)
dotnet run --project solutions/RxSpatial.Benchmarks -c Release -- --filter "*Geo*"
dotnet run --project solutions/RxSpatial.Experiments -c Release -- all     # ICDE 2017 Figs 6–12, ~45 min
```

## Workflow rules

- IMPORTANT: never push without being asked. `origin` is `endjin/RxSpatial`. The code is MIT
  (`LICENSE`, matching upstream `williamshyy/RxSpatial` since Oct 2026); upstream's `RTree/` and
  `RUMTree/` are LGPL 2.1 — never re-vendor them (`RxSpatial.Index` is their clean-room replacement).
- Default branch is `main`. Commit each logical step separately; stage only the files you
  changed (the user often has unrelated uncommitted edits, e.g. IDE table reformatting).
- Definition of done for code changes: warning-free `--no-incremental` Release build, all tests
  green, Examples smoke test passes; for changes to cross-platform projects also the Linux
  Docker check. Report counts, don't just say "passed".
- Breaking public API changes go in `CHANGELOG.md` (Keep a Changelog, `[Unreleased]`).
  Performance/memory work goes in `docs/performance.md` with same-session before/after numbers.
- Windows FS is case-insensitive: case-only renames need `git mv`.

## Layout

- Root: build/CI (`build.ps1`, `.zf/`, `.github/`, `global.json`, `GitVersion.yml`), `.editorconfig`, docs.
  `solutions/`: the slnx/slnf, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`,
  and one folder per project (flat; the slnx groups them into `src`/`samples`/`tests`/`benchmarks`
  solution folders). Build output goes to `solutions/artifacts/`.
- `solutions/RxSpatial` — core: facades, engine, backends, options, Rx extensions.
- `solutions/RxSpatial.Geo` — zero-dep clean-room shapes and maths (geodetic + planar).
- `solutions/RxSpatial.Index` — zero-dep R*-tree + RUM-tree.
- `solutions/archive/RxSpatial.SqlServer` — Windows-only `RxSqlGeography`/`RxSqlGeometry` over Microsoft.SqlServer.Types.
- `solutions/RxSpatial.Examples` (any OS), `solutions/MapRunRxSpatial` (WPF + WebView2 demo).
- `solutions/RxSpatial.Tests` (any OS), `solutions/archive/RxSpatial.SqlServer.Tests` (Windows), `solutions/RxSpatial.Tests/Golden/*.json`.
- `solutions/RxSpatial.Benchmarks`, `solutions/archive/RxSpatial.SqlServer.Benchmarks` (Windows) — BenchmarkDotNet suites;
  `solutions/RxSpatial.Experiments` — the ICDE experiments harness.
- `solutions/archive/` — the Windows-only SQL Server projects (`RxSpatial.SqlServer`, `.Tests`, `.Benchmarks`).
  They are not in `RxSpatial.slnx` (so not in CI, the SBOM or the test count); `archive/RxSpatial.Archive.slnx`
  holds them. They use engine internals (`InternalsVisibleTo`): after changing the engine or backends, run:
  `dotnet test --project solutions/archive/RxSpatial.SqlServer.Tests/RxSpatial.SqlServer.Tests.csproj -c Release`
  (~92 tests; 1 golden recorder skipped by design).
- `docs/prototypes` — never-compiled 2016 archives: do not modernise.
- `solutions/RxSpatial.CrossPlatform.slnf` lists every cross-platform project: add new ones to it AND the slnx.

## Architecture decisions (not obvious from the code)

- `RxGeography` (over `GeoShape`) and `RxGeometry` (over `PlanarShape`) are thin facades over one
  internal engine, `MovingObjectCore<TShape, TOps>`. Shape knowledge lives only in
  `ISpatialOperations<TShape>` structs with static abstract members (JIT-specialised calls, no
  virtual dispatch). `RxSpatial.SqlServer` reaches the engine via `InternalsVisibleTo`.
- Backends (`ListBackend`, `GridBackend`, `TreeBackend` over `RxSpatial.Index`) are shape-agnostic:
  they index `TOps.BoundsOf(shape)` as a `Rect`. Chosen by `RxSpatialOptions.PreferredDataStruct`
  (the enum `RxGeographyDataStruct` keeps its historic name and serves both types).
- Two grid settings: `Grid` (lat/long) for `RxGeography`, `PlanarGrid` (X/Y) for `RxGeometry`.
- Mixing `RxGeography` and `RxGeometry` is a compile error by design (no shared base type).
- Distances are `double`; NaN when no distance exists (empty shape, non-converging geodesic,
  SQL Null). NaN fails every comparison, so classifiers must test for it first.
- Fidelity against SQL Server is pinned by `solutions/RxSpatial.Tests/Golden/*.json` (see `.claude/rules/geo.md`).

## Gotchas

- `RxSpatialOptions.Default` is process-global, so tests run sequentially by design.
- `Location` is null until the first `OnNext`, so RxRx subscriptions before a first push fail
  on tree/grid backends (preserved 2016 behaviour). Tree-backend zone tests push small discs,
  not bare points.
- Empty shapes pushed into or subscribed on Tree/Grid backends throw (`Bounds` has nothing to
  index); only the List backend accepts them.
- Backends own the location-tracker subscription they open in `Subscribe(observer, source)` and
  must dispose it in `Remove`, or the next push re-registers the observer (`Update` is an
  upsert). Grid `placements` must record the whole cell block of every observer. Guarded by
  `BackendSubscriptionTests`.
- Rx's `Observable.Create` detaches observers on dispose, hiding subscription-lifetime bugs:
  test lifetimes on the raw `Rx*`/`RxRx*` API, not the observable extensions.
- Analyzer bar: `AnalysisMode=Recommended` + CA1305/CA2000/CA5392/CA1852, TreatWarningsAsErrors
  in Release. Exemptions go in `.editorconfig` with a reason, never in code. Verify analyzer
  changes with `--no-incremental` — an up-to-date build reports nothing.
- Package versions live only in `Directory.Packages.props` (CPM), except MSTest: `MSTest.Sdk` in
  `global.json` is its single version knob. Never add `PackageVersion` Include/Update for
  `MSTest.*`/`Microsoft.Testing.*` (Include → NU1506; Update in a csproj is a no-op). Visual
  Studio's package-update UI writes those Update blocks into the test csproj — delete them.
  Versions only the archived projects use (`Microsoft.SqlServer.Types`) live in
  `solutions/archive/Directory.Packages.props`, which imports the main one.
- Tests use Microsoft.Testing.Platform (global.json): `dotnet test` needs `--project <csproj>` or
  `--solution <slnx|slnf>`, not a positional path.
- Never build the slnx, `*.SqlServer*` or the WPF project on Linux/macOS — use the `.slnf`.
- A hung `dotnet test` under a coverage collector is usually stale MSBuild nodes:
  `dotnet build-server shutdown`.
