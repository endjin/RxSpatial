using RxSpatial.SqlServer;

namespace RxSpatial.Experiments;

/// <summary>
/// The load-shedding figures of ICDE 2017 §VI-D: Fig. 11 (dropped updates against a 5–40 ms
/// inter-arrival time) and Fig. 12 (dropped updates against the number of moving objects).
/// An update is dropped when the moving object's input buffer is full and
/// <see cref="RxSqlGeography.OnNext"/> throws <see cref="RxSpatialOverloadException"/>.
/// </summary>
internal static class SheddingExperiments
{
    /// <summary>
    /// Fig. 11: "Similar to Section VI-A, this experiment registers 2000 RxIntersects operations
    /// on top of a single moving object", at 5–40 ms. The update count per point is not stated.
    /// 0.1–2 ms are added below the paper's axis to find where this machine starts shedding.
    /// </summary>
    public static void Figure11(ExperimentContext context)
    {
        double[] beyond = [0.1, 0.5, 1, 2];
        double[] intervals = [.. beyond, 5, 10, 15, 20, 25, 30, 35, 40];
        int updates = context.Scale(200, 1000);
        ExperimentContext.Log(FormattableString.Invariant($"fig11: 1 object, {ExperimentContext.PaperFences} fences, {updates} updates per inter-arrival time"));

        Dictionary<(Variant, double), StreamResult> results = LatencyExperiments.SingleObjectSweep(
            context, ExperimentContext.PaperFences, intervals, updates);
        context.Report.WriteCsv("fig11", LatencyExperiments.StreamHeader("interval_ms"), LatencyExperiments.Rows(results, key => key));

        string table = Report.Table(
            ["Inter-arrival (ms)", .. ExperimentContext.Variants.Select(v => v.Label), "Achieved (ms, SqlSpatial)"],
            intervals.Select(i => (IReadOnlyList<object>)
            [
                beyond.Contains(i) ? FormattableString.Invariant($"{i} (beyond paper)") : i,
                .. ExperimentContext.Variants.Select(v => (object)results[(v, i)].Dropped),
                results[(Variant.SqlSpatial, i)].AchievedIntervalMs,
            ]));

        context.Report.Section(FormattableString.Invariant($"""
            ## Fig. 11 — dropped updates vs location update inter-arrival time

            One moving object, {ExperimentContext.PaperFences} `RxIntersect` geofences, input buffer {ExperimentContext.InputBuffer}, {updates} updates per point (variants interleaved). Dropped location updates:

            {table}
            | Paper claim | Paper | Measured |
            |---|---|---|
            | No drops above … | R-Tree 15 ms, RUM-Tree 10 ms | {string.Join("; ", ExperimentContext.Variants.Select(v => $"{v.Label} {DropFreeFrom(results, v, intervals)}"))} |
            | SqlSpatial drops most, Grid next | yes | totals: {string.Join(", ", ExperimentContext.Variants.Select(v => FormattableString.Invariant($"{v.Label} {intervals.Sum(i => results[(v, i)].Dropped)}")))} |

            Cross-check of query answers: {LatencyExperiments.CrossCheck(results)}.
            """));
    }

    /// <summary>
    /// Fig. 12: "each moving object sends 200 requests to 50 Observers at an interval of 50ms"
    /// (10,000 location updates per object), 1 → 20 objects, input buffer 10. The observers are
    /// taken to be <c>RxIntersect</c> geofences, as in the rest of §VI-D.
    /// </summary>
    public static void Figure12(ExperimentContext context)
    {
        int[] counts = context.Scale<int[]>([1, 5, 10, 15, 20], [.. Enumerable.Range(1, 20)]);
        int observers = 50;
        int requests = 200;
        double interval = 50;
        ExperimentContext.Log(FormattableString.Invariant($"fig12: 1–{counts[^1]} objects × {observers} observers, {requests} requests at {interval} ms"));

        Dictionary<Variant, Fleet> fleets = ExperimentContext.Variants.ToDictionary(
            v => v, v => new Fleet(v, context.Workload, observers, ExperimentContext.InputBuffer, requests));
        Dictionary<(Variant, double), StreamResult> results = [];
        try
        {
            foreach (Fleet fleet in fleets.Values)
                StreamRunner.Run(fleet.Take(counts[^1]), 20, 5);

            foreach (int count in counts)
            {
                foreach (Variant variant in ExperimentContext.Variants)
                {
                    StreamResult result = StreamRunner.Run(fleets[variant].Take(count), interval, requests);
                    results[(variant, count)] = result;
                    ExperimentContext.Log(LatencyExperiments.Describe(variant, result));
                }
            }
        }
        finally
        {
            foreach (Fleet fleet in fleets.Values)
                fleet.Dispose();
        }

        context.Report.WriteCsv("fig12", LatencyExperiments.StreamHeader("objects"), LatencyExperiments.Rows(results, key => key));
        string table = Report.Table(
            ["Objects", "Location updates (requests × observers)", .. ExperimentContext.Variants.Select(v => v.Label + " dropped (peak buffer)")],
            counts.Select(c => (IReadOnlyList<object>)
            [
                c, c * requests * observers,
                .. ExperimentContext.Variants.Select(v => (object)FormattableString.Invariant($"{results[(v, c)].Dropped * observers} ({results[(v, c)].PeakQueue})")),
            ]));

        context.Report.Section(FormattableString.Invariant($"""
            ## Fig. 12 — dropped updates vs number of moving objects

            Each object sends {requests} requests at {interval} ms to {observers} `RxIntersect` observers; input buffer {ExperimentContext.InputBuffer}; variants interleaved per object count. Dropped location updates are counted in the paper's unit — a dropped request drops one update for each of its {observers} observers — with the deepest input buffer seen in brackets:

            {table}
            | Paper claim ({counts[^1] * requests * observers:N0} updates) | Paper | Measured |
            |---|---|---|
            | Trees drop 190–200% fewer than SqlSpatial and Grid | up to ~4000 dropped for SqlSpatial | {string.Join(", ", ExperimentContext.Variants.Select(v => FormattableString.Invariant($"{v.Label} {results[(v, counts[^1])].Dropped * observers}")))} |

            Cross-check of query answers: {LatencyExperiments.CrossCheck(results)}.
            """));
    }

    // The smallest inter-arrival time from which no larger one dropped anything.
    private static string DropFreeFrom(Dictionary<(Variant, double), StreamResult> results, Variant variant, double[] intervals)
    {
        double? from = null;
        for (int i = intervals.Length - 1; i >= 0 && results[(variant, intervals[i])].Dropped == 0; i--)
            from = intervals[i];

        return from switch
        {
            null => "drops at every point",
            double f when f == intervals[0] => FormattableString.Invariant($"none at any point (≥ {f} ms)"),
            double f => FormattableString.Invariant($"none from {f} ms"),
        };
    }
}
