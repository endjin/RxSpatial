using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace RxSpatial.Experiments;

/// <summary>
/// Reproduces the experimental evaluation (§VI) of the ICDE 2017 RxSpatial paper with real,
/// paced location-update streams. Unlike the BenchmarkDotNet suite, which times one operation
/// in isolation, these experiments measure the paper's system-level measures — latency through
/// the input buffer, maximum load and dropped updates — so they run in wall-clock time.
/// </summary>
internal static class Program
{
    private static readonly Dictionary<string, Action<ExperimentContext>> Experiments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["service"] = context => ServiceTimeExperiment.Run(context),
        ["fig6"] = LatencyExperiments.Figure6,
        ["fig7"] = LoadExperiments.Figure7,
        ["fig8"] = LoadExperiments.Figure8,
        ["fig9"] = LatencyExperiments.Figure9,
        ["fig10"] = LatencyExperiments.Figure10,
        ["fig11"] = SheddingExperiments.Figure11,
        ["fig12"] = SheddingExperiments.Figure12,
    };

    private static int Main(string[] args)
    {
        List<string> names = [];
        bool full = false;
        int seed = 42;
        int? cap = null;
        string? output = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--full":
                    full = true;
                    break;
                case "--seed" when i + 1 < args.Length:
                    seed = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--max-objects" when i + 1 < args.Length:
                    cap = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                case "--out" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "all":
                    names.AddRange(Experiments.Keys);
                    break;
                case string name when Experiments.ContainsKey(name):
                    names.Add(name);
                    break;
                default:
                    return Usage($"Unknown argument '{args[i]}'.");
            }
        }

        if (names.Count == 0)
            return Usage(null);

        output ??= DefaultOutput();
        Directory.CreateDirectory(output);
        Report report = new(output);
        ExperimentContext context = new(full, seed, cap ?? (full ? 4096 : 1024), report);
        report.Section(Header(context, names));
        ExperimentContext.Log($"Writing to {output}");

        foreach (string name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            long start = Stopwatch.GetTimestamp();
            Experiments[name](context);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
            ExperimentContext.Log(FormattableString.Invariant($"{name} finished in {elapsed:hh\\:mm\\:ss}"));
            report.Section(FormattableString.Invariant($"_{name}: {elapsed:hh\\:mm\\:ss} wall-clock._"));
        }

        ExperimentContext.Log($"Done. Summary: {Path.Combine(output, "summary.md")}");
        return 0;
    }

    private static string Header(ExperimentContext context, List<string> names)
        => FormattableString.Invariant($"""
            # RxSpatial — ICDE 2017 §VI experiments

            - Run: {DateTime.Now:yyyy-MM-dd HH:mm}, `{string.Join(' ', names)}`, {(context.Full ? "full (`--full`)" : "default (quick) scale")}, seed {context.Seed}, object cap {context.ObjectCap}
            - Machine: {Environment.ProcessorCount} logical processors, {RuntimeInformation.OSDescription}, {RuntimeInformation.FrameworkDescription}
            - Workload: Seattle area ({SeattleWorkload.MinLatitude}–{SeattleWorkload.MaxLatitude} N, {SeattleWorkload.MinLongitude}–{SeattleWorkload.MaxLongitude} E), exact `GeoCircle` geofences of 100–400 m (75% clustered round 25 hot spots), random-walk routes of bare points; buffered mode, input buffer {ExperimentContext.InputBuffer}; Grid = the default 10×10 grid over 46–49 N, 124–120 W
            - "SqlSpatial" = the List backend (the paper's own "List based (SQLSpatial)"); latency = `RxGeography.AverageLatency`, input buffer → results propagated
            """);

    // artifacts/experiments/<timestamp> under the repository root, or %TEMP% outside the repository.
    private static string DefaultOutput()
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RxSpatial.slnx")))
                return Path.Combine(directory.FullName, "artifacts", "experiments", stamp);
        }

        return Path.Combine(Path.GetTempPath(), "rxspatial-experiments", stamp);
    }

    private static int Usage(string? error)
    {
        if (error is not null)
            Console.Error.WriteLine(error);

        Console.WriteLine("""
            Usage: RxSpatial.Experiments <experiment>... [--full] [--out <dir>] [--seed <n>] [--max-objects <n>]

            Experiments: service, fig6, fig7, fig8, fig9, fig10, fig11, fig12, all
              --full          the paper's update counts and every axis point (default: a quick, reduced sweep)
              --out           output folder (default: artifacts/experiments/<timestamp>)
              --seed          workload seed (default 42)
              --max-objects   cap for the maximum-load searches (default 1024; 4096 with --full)
            """);
        return error is null ? 0 : 1;
    }
}
