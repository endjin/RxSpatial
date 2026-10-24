// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial.Experiments;

/// <summary>
/// The latency figures of ICDE 2017 §VI: Fig. 6 (inter-arrival time), Fig. 9 (observers per
/// object) and Fig. 10 (number of moving objects). Latency is the library's own measure,
/// <see cref="RxGeography.AverageLatency"/>: from the update entering the input buffer until
/// its results have been pushed to the output observers.
/// </summary>
internal static class LatencyExperiments
{
    /// <summary>
    /// Fig. 6: "2000 RxIntersects operations on top of a single moving object", latency against
    /// the location update inter-arrival time (100–900 ms on the figure's axis).
    /// </summary>
    public static void Figure6(ExperimentContext context)
    {
        double[] intervals = [100, 200, 300, 400, 500, 600, 700, 800, 900];
        int updates = context.Scale(10, 50);
        ExperimentContext.Log(FormattableString.Invariant($"fig6: 1 object, {ExperimentContext.PaperFences} fences, {updates} updates per inter-arrival time"));

        Dictionary<(Variant, double), StreamResult> results = SingleObjectSweep(
            context, ExperimentContext.PaperFences, intervals, updates);

        context.Report.WriteCsv("fig6", StreamHeader("interval_ms"), Rows(results, key => key));

        StreamResult At(Variant variant, double interval) => results[(variant, interval)];
        double sql = At(Variant.SqlSpatial, 200).AverageLatencyMs;
        double rum = At(Variant.RumTree, 200).AverageLatencyMs;
        double rtree = At(Variant.RTree, 200).AverageLatencyMs;
        double grid = At(Variant.Grid, 200).AverageLatencyMs;
        double meanRum = intervals.Average(i => At(Variant.RumTree, i).AverageLatencyMs);
        double meanRtree = intervals.Average(i => At(Variant.RTree, i).AverageLatencyMs);
        double meanGrid = intervals.Average(i => At(Variant.Grid, i).AverageLatencyMs);

        context.Report.Section(FormattableString.Invariant($"""
            ## Fig. 6 — latency vs location update inter-arrival time

            One moving object, {ExperimentContext.PaperFences} `RxIntersect` geofences, input buffer {ExperimentContext.InputBuffer}, {updates} updates per point; variants interleaved per inter-arrival time. Average latency (ms):

            {LatencyTable(results, intervals, "Inter-arrival (ms)")}
            | Paper claim | Paper | Measured |
            |---|---|---|
            | RUM-Tree vs SqlSpatial at 200 ms | ~55× lower latency | {sql / rum:F1}× |
            | RUM-Tree vs R-Tree latency | 13.3% lower | {Change(rum, rtree)} at 200 ms; {Change(meanRum, meanRtree)} averaged over all points |
            | RUM-Tree vs Grid latency | 41.7% lower | {Change(rum, grid)} at 200 ms; {Change(meanRum, meanGrid)} averaged over all points |

            Cross-check of query answers: {CrossCheck(results)}.
            """));
    }

    /// <summary>
    /// Fig. 9: latency against the number of stream observers per object (0–100 on the axis).
    /// The paper gives neither the inter-arrival time nor the object count; this uses one
    /// object at 200 ms, Fig. 6's headline rate.
    /// </summary>
    public static void Figure9(ExperimentContext context)
    {
        int[] observers = context.Scale<int[]>([10, 25, 50, 75, 100], [10, 20, 30, 40, 50, 60, 70, 80, 90, 100]);
        double interval = 200;
        int updates = context.Scale(10, 50);
        ExperimentContext.Log(FormattableString.Invariant($"fig9: 1 object at {interval} ms, {updates} updates per observer count"));

        Dictionary<(Variant, double), StreamResult> results = [];
        foreach (int count in observers)
        {
            Dictionary<(Variant, double), StreamResult> sweep = SingleObjectSweep(context, count, [interval], updates);
            foreach (Variant variant in ExperimentContext.Variants)
                results[(variant, count)] = sweep[(variant, interval)];
        }

        context.Report.WriteCsv("fig9", StreamHeader("observers"), Rows(results, key => key));
        double[] keys = [.. observers.Select(o => (double)o)];
        double last = keys[^1];
        double rum = results[(Variant.RumTree, last)].AverageLatencyMs;

        context.Report.Section(FormattableString.Invariant($"""
            ## Fig. 9 — latency vs stream observers per object

            One moving object at a {interval} ms inter-arrival time (the paper does not state its rate), `RxIntersect` observers on the first N geofences, {updates} updates per point. Average latency (ms):

            {LatencyTable(results, keys, "Observers")}
            | Paper claim (100 observers) | Paper | Measured |
            |---|---|---|
            | RUM-Tree vs R-Tree | 11.2% lower | {Change(rum, results[(Variant.RTree, last)].AverageLatencyMs)} |
            | RUM-Tree vs Grid | 62.81% lower | {Change(rum, results[(Variant.Grid, last)].AverageLatencyMs)} |
            | RUM-Tree vs SqlSpatial (List) | 94.22% lower | {Change(rum, results[(Variant.SqlSpatial, last)].AverageLatencyMs)} |

            Cross-check of query answers: {CrossCheck(results)}.
            """));
    }

    /// <summary>
    /// Fig. 10: latency against the number of moving objects (200–1000). The paper gives
    /// neither the rate nor the observers per object. This uses 500 geofences per object at a
    /// 1000 ms inter-arrival time, which puts the List variant's saturation point (cores × T ÷
    /// ~25 ms per update) inside the 200–1000 range, where the paper's curves diverge.
    /// </summary>
    public static void Figure10(ExperimentContext context)
    {
        int[] counts = [200, 400, 600, 800, 1000];
        double interval = 1000;
        int fences = 500;
        int updates = context.Scale(5, 20);
        ExperimentContext.Log(FormattableString.Invariant($"fig10: up to {counts[^1]} objects × {fences} fences at {interval} ms, {updates} updates each"));

        Dictionary<(Variant, double), StreamResult> results = [];
        foreach (Variant variant in ExperimentContext.Variants)
        {
            using Fleet fleet = new(variant, context.Workload, fences, ExperimentContext.InputBuffer, updates + 1);
            StreamRunner.Run(fleet.Take(counts[^1]), 20, 1); // warm-up: JIT and first-touch of every object
            ExperimentContext.Log(FormattableString.Invariant($"  {variant.Label}: built {counts[^1]} objects in {fleet.BuildTime.TotalSeconds:F1} s"));
            foreach (int count in counts)
            {
                StreamResult result = StreamRunner.Run(fleet.Take(count), interval, updates);
                results[(variant, count)] = result;
                ExperimentContext.Log(Describe(variant, result));
            }
        }

        context.Report.WriteCsv("fig10", StreamHeader("objects"), Rows(results, key => key));
        double[] keys = [.. counts.Select(c => (double)c)];
        double rum = results[(Variant.RumTree, 1000)].AverageLatencyMs;

        context.Report.Section(FormattableString.Invariant($"""
            ## Fig. 10 — latency vs number of moving objects

            N moving objects × {fences} `RxIntersect` geofences each at a {interval} ms inter-arrival time (neither is stated in the paper), {updates} updates per object. Average latency (ms):

            {LatencyTable(results, keys, "Objects")}
            | Paper claim (1K objects) | Paper | Measured |
            |---|---|---|
            | RUM-Tree vs R-Tree | 12.2% lower | {Change(rum, results[(Variant.RTree, 1000)].AverageLatencyMs)} |
            | RUM-Tree vs Grid | 37.22% lower | {Change(rum, results[(Variant.Grid, 1000)].AverageLatencyMs)} |
            | RUM-Tree vs SqlSpatial | 54.95% lower | {Change(rum, results[(Variant.SqlSpatial, 1000)].AverageLatencyMs)} |
            | Light load (~200 objects) | comparable across variants | SqlSpatial ÷ RUM-Tree = {results[(Variant.SqlSpatial, 200)].AverageLatencyMs / results[(Variant.RumTree, 200)].AverageLatencyMs:F1}× |

            Cross-check of query answers (concurrent drainers sharing the fences): {CrossCheck(results)}.
            """));
    }

    /// <summary>
    /// Runs one object per variant through each inter-arrival time, interleaving the variants
    /// at every point so that drift in machine load hits them alike.
    /// </summary>
    public static Dictionary<(Variant, double), StreamResult> SingleObjectSweep(
        ExperimentContext context, int fences, double[] intervals, int updates)
    {
        int warmup = 5;
        Dictionary<Variant, Fleet> fleets = context.SingleObjectFleets(fences, warmup + (intervals.Length * updates));
        try
        {
            foreach (Fleet fleet in fleets.Values)
                StreamRunner.Run(fleet.Take(1), 20, warmup);

            Dictionary<(Variant, double), StreamResult> results = [];
            foreach (double interval in intervals)
            {
                foreach (Variant variant in ExperimentContext.Variants)
                {
                    StreamResult result = StreamRunner.Run(fleets[variant].Take(1), interval, updates);
                    results[(variant, interval)] = result;
                    ExperimentContext.Log(Describe(variant, result));
                }
            }

            return results;
        }
        finally
        {
            foreach (Fleet fleet in fleets.Values)
                fleet.Dispose();
        }
    }

    public static string[] StreamHeader(string key)
        => ["variant", key, "objects", "requested_interval_ms", "achieved_interval_ms", "attempted", "dropped", "avg_latency_ms", "mean_producer_lateness_ms", "max_producer_lateness_ms", "peak_queue", "evaluations_per_update", "matches"];

    public static IEnumerable<IReadOnlyList<object>> Rows(Dictionary<(Variant, double), StreamResult> results, Func<double, object> key)
        => results.Select(r => (IReadOnlyList<object>)
        [
            r.Key.Item1.Label, key(r.Key.Item2), r.Value.Objects, r.Value.RequestedIntervalMs, r.Value.AchievedIntervalMs,
            r.Value.Attempted, r.Value.Dropped, r.Value.AverageLatencyMs, r.Value.MeanLatenessMs, r.Value.MaxLatenessMs,
            r.Value.PeakQueue, r.Value.EvaluationsPerUpdate, r.Value.Matches,
        ]);

    public static string Describe(Variant variant, StreamResult result)
        => FormattableString.Invariant(
            $"  {variant.Label,-20} n={result.Objects,-5} T={result.RequestedIntervalMs,6:F1} ms (achieved {result.AchievedIntervalMs:F2}) latency={result.AverageLatencyMs:F4} ms dropped={result.Dropped}/{result.Attempted} peakQ={result.PeakQueue} eval/upd={result.EvaluationsPerUpdate:F1}");

    /// <summary>
    /// At every point all variants were offered the same updates, so those that dropped none
    /// must report the same number of "inside" answers: the indexes may only prune observers
    /// that cannot match. Runs with drops processed a different subset and are left out.
    /// </summary>
    public static string CrossCheck(Dictionary<(Variant, double), StreamResult> results)
    {
        IEnumerable<IGrouping<double, StreamResult>> points = results.GroupBy(r => r.Key.Item2, r => r.Value);
        int mismatches = points.Count(p => p.Where(r => r.Dropped == 0).Select(r => r.Matches).Distinct().Count() > 1);
        int excluded = results.Values.Count(r => r.Dropped > 0);
        string verdict = mismatches == 0
            ? "every variant gave identical answers at every point"
            : FormattableString.Invariant($"**MISMATCH at {mismatches} point(s)**");
        return excluded == 0 ? verdict : FormattableString.Invariant($"{verdict} ({excluded} runs with drops not compared)");
    }

    /// <summary>How <paramref name="value"/> compares with <paramref name="baseline"/>, e.g. "13.3% lower".</summary>
    public static string Change(double value, double baseline)
    {
        double decrease = (baseline - value) / baseline * 100;
        return decrease >= 0
            ? FormattableString.Invariant($"{decrease:F1}% lower")
            : FormattableString.Invariant($"{-decrease:F1}% higher");
    }

    private static string LatencyTable(Dictionary<(Variant, double), StreamResult> results, double[] keys, string keyName)
        => Report.Table(
            [keyName, .. ExperimentContext.Variants.Select(v => v.Label)],
            keys.Select(k => (IReadOnlyList<object>)[k, .. ExperimentContext.Variants.Select(v => (object)results[(v, k)].AverageLatencyMs)]));
}
