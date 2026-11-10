# Repository layout

The repository root holds the build and CI files and the documentation; everything .NET (the solution, the shared `Directory.Build.props` and `Directory.Packages.props`, and the projects below) lives under `solutions/`. Open [`solutions/RxSpatial.slnx`](../solutions/RxSpatial.slnx). The SQL Server projects are archived in `solutions/archive/`, outside `RxSpatial.slnx`, so neither a solution build nor CI touches them; open or build them with [`solutions/archive/RxSpatial.Archive.slnx`](../solutions/archive/RxSpatial.Archive.slnx).

| Path | Platforms | Description |
| --- | --- | --- |
| `solutions/RxSpatial` | Windows, Linux, macOS | The core library: `RxGeography` (geodetic) and `RxGeometry` (planar) moving objects, continuous distance / intersection / intersect queries, and pluggable spatial index backends (list, grid, R*-tree, RUM-tree). |
| `solutions/RxSpatial.Geo` | Windows, Linux, macOS | The shapes and shape maths the core uses: WGS84 points, exact circles and convex polygons (`GeoShape`), their planar twins (`PlanarShape`), and `GeoOperations` / `PlanarOperations` (intersects, intersection, distance, area, bounds). Clean-room, zero dependencies. |
| `solutions/RxSpatial.Index` | Windows, Linux, macOS | R*-tree (Beckmann et al., 1990) and RUM-tree (R-tree with Update Memo, Xiong &amp; Aref) spatial indexes sharing one engine; a clean-room .NET 10 replacement for the formerly vendored LGPL Java ports. Zero dependencies. |
| `solutions/archive/RxSpatial.SqlServer` | Windows only (archived) | `RxSqlGeography` and `RxSqlGeometry`: the same moving objects over Microsoft.SqlServer.Types' `SqlGeography` / `SqlGeometry`, for code that must keep SQL Server's types or results (see [SQL Server types](sql-server.md)). |
| `solutions/RxSpatial.Examples` | Windows, Linux, macOS | Runnable example / smoke test: Rx.NET v7 sequences feeding and consuming RxSpatial streams on every backend. |
| `solutions/MapRunRxSpatial` | Windows only (WPF) | WPF demo that visualises moving objects and continuous queries on a MapLibre GL JS map hosted in WebView2 (OpenStreetMap tiles, no API key). |
| `solutions/RxSpatial.Benchmarks` | Windows, Linux, macOS | BenchmarkDotNet suite: per-backend location-update cost, the RxSpatial.Geo operations, geodesic primitives, polygon construction, outlines and multi-core scaling, plus R*-tree vs RUM-tree bulk-insert / update / query comparisons with allocation statistics. |
| `solutions/archive/RxSpatial.SqlServer.Benchmarks` | Windows only (archived) | The SQL Server baselines: the per-backend workloads through `RxSqlGeography` / `RxSqlGeometry`, and each shape operation on SQL Server against RxSpatial.Geo. |
| `solutions/RxSpatial.Experiments` | Windows, Linux, macOS | Wall-clock harness that reproduces the ICDE 2017 paper's experiments (Figs. 6–12): latency, maximum load and dropped updates under paced location-update streams. |
| `solutions/RxSpatial.Tests` | Windows, Linux, macOS | MSTest (MSTest.Sdk + Shouldly) tests for the core, the indexes and RxSpatial.Geo, including GeographicLib oracle tests and the fidelity tests against the recorded SQL Server values in `solutions/RxSpatial.Tests/Golden`. |
| `solutions/archive/RxSpatial.SqlServer.Tests` | Windows only (archived) | Tests for `RxSqlGeography` / `RxSqlGeometry`, the recorder of `solutions/RxSpatial.Tests/Golden/*.json`, and a guard that live SQL Server still matches it. |
| `docs/prototypes` | — | Never-compiled 2016 prototype sources, archived with provenance notes. |
