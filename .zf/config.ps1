<#
RxSpatial build process, using the 'ZeroFailed.Build.DotNet' extension to provide the features
needed when building a .NET solution: compile, test (Microsoft.Testing.Platform, under
dotnet-coverage), test/coverage reports, Covenant SBOM analysis and packaging.
#>

$zerofailedExtensions = @(
    @{
        # References the extension from its GitHub repository. If not already installed, use latest version from 'main' will be downloaded.
        Name = "ZeroFailed.Build.DotNet"
        GitRepository = "https://github.com/zerofailed/ZeroFailed.Build.DotNet"
        GitRef = "main"
    }
    @{
        # Creates the GitHub release and attaches the NuGet packages to it (the Publish stage).
        Name = "ZeroFailed.Build.GitHub"
        GitRepository = "https://github.com/zerofailed/ZeroFailed.Build.GitHub"
        GitRef = "main"
    }
)

# Load the tasks and process
. ZeroFailed.tasks -ZfPath $here/.zf

#
# Build process control options
#
$SkipInit = $false
$SkipVersion = $false
$SkipBuild = $false
$CleanBuild = $Clean
$SkipTest = $false
$SkipTestReport = $false
$SkipAnalysis = $false
$SkipPackage = $false
# The Publish stage runs only when the entry task is FullBuildAndPublish, which CI uses for tag
# builds (and for a manual run with forcePublish). It creates a GitHub release; see below.
$SkipPublish = $false

#
# Build process configuration
#

# The solution holds one Windows-only project, the WPF demo (the archived SQL Server projects are not in it).
# Linux and macOS CI legs point this at 'solutions/RxSpatial.CrossPlatform.slnf' instead. This
# environment variable is defined by this file, not by ZeroFailed.
$SolutionToBuild = (Resolve-Path (Join-Path $here (property ZF_BUILD_DOTNET_SOLUTION_TO_BUILD "solutions/RxSpatial.slnx"))).Path
$ProjectsToPublish = @()
$NuSpecFilesToPackage = @()
$NugetPublishSource = property ZF_NUGET_PUBLISH_SOURCE "$here/_local-nuget-feed"

# Publishing: the NuGet packages of the three libraries (RxSpatial, RxSpatial.Geo, RxSpatial.Index;
# the only projects with IsPackable=true) are attached to a GitHub release named after the build's
# version. They are not pushed to a NuGet feed: clearing the glob turns off the .NET extension's
# PublishNuGetPackages task, and the GitHub extension then falls back to the same default glob
# ('*.<SemVer>.nupkg' in $PackagesDir) to find the packages to attach.
$NugetPackageNamesToPublishGlob = $null
$PublishNuGetPackagesAsGitHubReleaseArtefacts = $true
# Only a GitHub Actions run creates a release, so a local 'FullBuildAndPublish' cannot. Dry-run mode
# (ZF_GITHUB_RELEASE_DRY_RUN_MODE=true) runs the task anywhere without creating or uploading anything.
$CreateGitHubRelease = ($env:GITHUB_ACTIONS -eq 'true') -or $GitHubReleaseDryRunMode

# Coverage covers the shipped libraries only, not the tests, benchmarks or samples.
$IncludeAssembliesInCodeCoverage = @(
    "RxSpatial"
    "RxSpatial.Geo"
    "RxSpatial.Index"
)

# Synopsis: Build, Test, Analyse and Package
task . FullBuild

#
# Build Process Extensibility Points - uncomment and implement as required
#

# task RunFirst {}
# task PreInit {}
# task PostInit {}
# task PreVersion {}
# task PostVersion {}
# task PreBuild {}
# task PostBuild {}
# task PreTest {}
task PostTest RunExamplesSmokeTest
# task PreTestReport {}
# task PostTestReport {}
# task PreAnalysis {}
# task PostAnalysis {}
# task PrePackage {}
# task PostPackage {}
# task PrePublish {}
# task PostPublish {}
# task RunLast {}

# Synopsis: Makes Covenant run a design-time build to find each project's restore output.
# Directory.Build.props sets UseArtifactsOutput, so project.assets.json lives under solutions/artifacts/obj, not
# <project>/obj where Covenant looks by default. ZeroFailed.Build.DotNet has no property for extra
# Covenant arguments, so this appends the flag to the argument list its PrepareCovenantMetadata task builds.
task AddCovenantDesignTimeBuildFlag -After PrepareCovenantMetadata {
    $script:covenantMetadataArgs += "--design-time-build"
}

# Synopsis: Runs the end-to-end examples console app, which exits non-zero unless every scenario passes.
task RunExamplesSmokeTest -If { !$SkipTest } {
    exec {
        dotnet run --project (Join-Path $here "solutions/RxSpatial.Examples") `
                   --configuration $Configuration `
                   --no-build
    }
}
