---
paths:
  - "build.ps1"
  - ".zf/**"
  - ".github/**"
  - "GitVersion.yml"
  - "Directory.Build.props"
  - "Directory.Packages.props"
  - "global.json"
  - "*.slnx"
  - "*.slnf"
  - "**/*.csproj"
---

# Build, CI and packaging

- CI (`.github/workflows/build.yml`) runs the endjin ZeroFailed build via the
  `run-build-process` action: Windows builds the full slnx; an ubuntu/macOS matrix runs
  `Build,Test` over `RxSpatial.CrossPlatform.slnf` (selected by `ZF_BUILD_DOTNET_SOLUTION_TO_BUILD`,
  a repo-specific variable read by `.zf/config.ps1`). The Examples smoke test is a `PostTest` task.
- ZeroFailed.Build.DotNet tracks `main` and auto-detects MTP, passing `--solution`,
  `--report-trx`, `--diagnostic*`. Never add VSTest-style `--logger`/`--collect` via
  `$AdditionalTestArgs`. Coverage comes from wrapping `dotnet test` in `dotnet-coverage collect`.
- Covenant (SBOM) looks for `project.assets.json` in `<project>/obj`, which `UseArtifactsOutput`
  moves; `.zf/config.ps1` hooks `AddCovenantDesignTimeBuildFlag` to pass `--design-time-build`.
- Packaging is off by design: `IsPackable=false` in Directory.Build.props and `$SkipPublish = $true`.
  To ship a library: `IsPackable=true` in its csproj, remove `$SkipPublish`, wire a feed and
  `NUGET_API_KEY` through `prepare-env-vars-and-secrets`.
- GitVersion 5.8 (ZeroFailed's pinned tool) has no `main` key: GitVersion.yml configures the
  default branch under `master:` with `regex: ^main`. No tags yet → `0.1.0-preview.<n>`.
- `auto_release.yml` tags `main` after any merged human PR unless labelled `no_release`; Dependabot
  PRs for actions/*, BenchmarkDotNet, MSTest.Sdk, Shouldly (patch/minor) auto-merge via
  `.github/config/pr-autoflow.json`; runtime dependencies are always reviewed.
- Windows-only code must stay isolated so it can be deprecated by deleting projects: Windows
  projects target `net10.0-windows` or carry `[SupportedOSPlatform("windows")]`, are excluded
  from the `.slnf`, and no cross-platform project may reference them.
