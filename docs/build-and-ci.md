# Build and CI

The CI build is the standard endjin [ZeroFailed](https://github.com/zerofailed/ZeroFailed) build for .NET: `build.ps1` runs the process configured in `.zf/config.ps1`, which uses the [ZeroFailed.Build.DotNet](https://github.com/zerofailed/ZeroFailed.Build.DotNet) extension. Run it locally with PowerShell 7:

```powershell
./build.ps1                                                   # full build: compile, test, analysis, package
./build.ps1 -Tasks Build,Test                                 # compile and test only
$env:ZF_BUILD_DOTNET_SOLUTION_TO_BUILD = 'solutions/RxSpatial.CrossPlatform.slnf'
./build.ps1 -Tasks Build,Test                                 # what the Linux and macOS CI jobs run
```

The first run installs the `InvokeBuild` and `ZeroFailed` PowerShell modules from the PowerShell Gallery, downloads the ZeroFailed extensions into `.zf/extensions`, and installs the .NET global tools that the build needs (GitVersion, dotnet-coverage, ReportGenerator, Covenant). The build: ZeroFailed 2 records the exact commit of each extension it installed in `.zf/extensions.lock.json`, which is committed. To move to newer extensions, delete `.zf/extensions`, run the build and commit the updated lock file.

1. **Version:** computes the version with GitVersion (`GitVersion.yml`). Until the first release tag, `main` builds `0.1.0-preview.<n>`.
2. **Build:** restores and builds the solution in Release. Warnings are errors.
3. **Test:** runs `dotnet test` (Microsoft.Testing.Platform) under `dotnet-coverage`, writes TRX files to `TestResults/`, then runs the examples smoke test. After the build, a Cobertura report and a Markdown summary for the four libraries go to `_codeCoverage/`.
4. **Analysis:** writes a [Covenant](https://github.com/patriksvensson/covenant) SBOM (`RxSpatial.sbom.json` and `.html`).
5. **Package:** runs `dotnet pack` over the solution and writes the NuGet packages to `_packages/`. Only the three libraries are packed (`RxSpatial`, `RxSpatial.Geo`, `RxSpatial.Index`): `Directory.Build.props` sets `IsPackable` to `false` for every project, and those three set it to `true` in their own project files. The tests, examples, benchmarks, experiments and the WPF demo are never packed.
6. **Publish:** runs only when the entry task is `FullBuildAndPublish`, which CI uses for a tag build (or a manual run with `forcePublish`). It creates a GitHub release named after the version and attaches the three packages to it, using the [ZeroFailed.Build.GitHub](https://github.com/zerofailed/ZeroFailed.Build.GitHub) extension. The build never pushes to a NuGet feed. Only a GitHub Actions run creates a release; to rehearse the stage locally without creating anything, run `$env:ZF_GITHUB_RELEASE_DRY_RUN_MODE = 'true'; ./build.ps1 -Tasks FullBuildAndPublish`.

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

