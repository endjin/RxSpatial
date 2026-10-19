using System.Runtime.CompilerServices;
using RxSpatial.Benchmarks.Shared;

namespace RxSpatial.Benchmarks;

/// <summary>
/// Opts every process that loads the benchmarks out of Windows power throttling — the host and
/// each BenchmarkDotNet child process, which loads this assembly and so runs this initializer
/// first. BenchmarkDotNet's children already run at high priority, which Windows does not
/// throttle (an A/B run measured identical times), so this guards the cases without it:
/// <c>--inProcess</c> runs under a profiler or coverage collector, and the host. See
/// <see cref="PowerThrottling"/>.
/// </summary>
internal static class PowerThrottlingInitializer
{
    [ModuleInitializer]
    internal static void Initialize() => PowerThrottling.OptOut();
}
