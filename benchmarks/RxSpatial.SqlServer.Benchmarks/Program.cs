using BenchmarkDotNet.Running;

namespace RxSpatial.SqlServer.Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
