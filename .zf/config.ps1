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
# Nothing is published yet: RxSpatial has never been released as a package. Keeping Publish skipped
# means a tag build (FullBuildAndPublish) can never push to a NuGet feed by accident.
$SkipPublish = $true

#
# Build process configuration
#

# The full solution holds Windows-only projects (RxSpatial.SqlServer, its tests and benchmarks, and the
# WPF demo). Linux and macOS CI legs point this at 'RxSpatial.CrossPlatform.slnf' instead. This
# environment variable is defined by this file, not by ZeroFailed.
$SolutionToBuild = (Resolve-Path (Join-Path $here (property ZF_BUILD_DOTNET_SOLUTION_TO_BUILD "RxSpatial.slnx"))).Path
$ProjectsToPublish = @()
$NuSpecFilesToPackage = @()
$NugetPublishSource = property ZF_NUGET_PUBLISH_SOURCE "$here/_local-nuget-feed"

# Coverage covers the shipped libraries only, not the tests, benchmarks or samples.
$IncludeAssembliesInCodeCoverage = @(
    "RxSpatial"
    "RxSpatial.Geo"
    "RxSpatial.Index"
    "RxSpatial.SqlServer"
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
# Directory.Build.props sets UseArtifactsOutput, so project.assets.json lives under artifacts/obj, not
# <project>/obj where Covenant looks by default. ZeroFailed.Build.DotNet has no property for extra
# Covenant arguments, so this appends the flag to the argument list its PrepareCovenantMetadata task builds.
task AddCovenantDesignTimeBuildFlag -After PrepareCovenantMetadata {
    $script:covenantMetadataArgs += "--design-time-build"
}

# Synopsis: Runs the end-to-end examples console app, which exits non-zero unless every scenario passes.
task RunExamplesSmokeTest -If { !$SkipTest } {
    exec {
        dotnet run --project (Join-Path $here "samples/RxSpatial.Examples") `
                   --configuration $Configuration `
                   --no-build
    }
}
