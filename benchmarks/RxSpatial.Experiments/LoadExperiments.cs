// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace RxSpatial.Experiments;

/// <summary>
/// The maximum-load figures of ICDE 2017 §VI: Fig. 7 (inter-arrival time) and Fig. 8
/// (observers per object). Maximum load is the most moving objects tracked without dropping
/// one incoming location update; see <see cref="MaxLoadSearch"/>.
/// </summary>
internal static class LoadExperiments
{
    /// <summary>
    /// Fig. 7: maximum load against the inter-arrival time (200–1400 ms on the axis, a 1500 ms
    /// summary in the text). The per-object workload is not stated; this keeps Fig. 6's 2000
    /// geofences per object.
    /// </summary>
    public static void Figure7(ExperimentContext context)
    {
        double[] intervals = context.Scale<double[]>([200, 600, 1000, 1500], [200, 400, 600, 800, 1000, 1200, 1400, 1500]);
        int rounds = context.Scale(20, 50);
        Dictionary<(Variant, double), MaxLoadResult> results = Sweep(
            context, "fig7", intervals, _ => ExperimentContext.PaperFences, i => i, rounds);

        double last = intervals[^1];
        MaxLoadResult rum = results[(Variant.RumTree, last)];
        context.Report.Section(FormattableString.Invariant($"""
            ## Fig. 7 — maximum load vs location update inter-arrival time

            Moving objects × {ExperimentContext.PaperFences} `RxIntersect` geofences each, input buffer {ExperimentContext.InputBuffer}; a probe of {rounds} updates per object passes if it drops nothing *and* its mean latency stays below the inter-arrival time (a backlogged system has not dropped yet only because the probe is finite; see `MaxLoadSearch.Passes`). Object cap {context.ObjectCap} ("≥" = never dropped up to the cap). Maximum load (moving objects):

            {LoadTable(results, intervals, "Inter-arrival (ms)")}
            | Paper claim ({last} ms) | Paper | Measured |
            |---|---|---|
            | Smaller inter-arrival → fewer objects | yes | {Trend(results, intervals)} |
            | SqlSpatial has the lowest maximum load | yes | {(intervals.All(i => ExperimentContext.Variants.All(v => results[(Variant.SqlSpatial, i)].MaxObjects <= results[(v, i)].MaxObjects)) ? "yes" : "no")} |
            | RUM-Tree over R-Tree | +3.7% | {Increase(rum, results[(Variant.RTree, last)])} |
            | RUM-Tree over Grid | +18% | {Increase(rum, results[(Variant.Grid, last)])} |
            | RUM-Tree over SqlSpatial (List) | +63% | {Increase(rum, results[(Variant.SqlSpatial, last)])} |
            """));
    }

    /// <summary>
    /// Fig. 8: maximum load against stream observers per moving object (20–160 on the axis).
    /// The paper states no inter-arrival time. With SQL Server's spatial types, 20–160 observers
    /// cost a List update ~1–8 ms (~50 µs per geography <c>STIntersects</c>), so at Fig. 6's
    /// 200 ms even the List variant sustained thousands of objects and 20 ms brought its knee
    /// within the object cap. On RxSpatial.Geo the same update costs microseconds, so every
    /// variant can reach the cap here.
    /// </summary>
    public static void Figure8(ExperimentContext context)
    {
        double interval = 20;
        int[] observers = context.Scale<int[]>([20, 40, 80, 160], [20, 40, 60, 80, 100, 120, 140, 160]);
        int rounds = context.Scale(20, 50);
        Dictionary<(Variant, double), MaxLoadResult> results = Sweep(
            context, "fig8", [.. observers.Select(o => (double)o)], o => (int)o, _ => interval, rounds);

        double[] keys = [.. observers.Select(o => (double)o)];
        context.Report.Section(FormattableString.Invariant($"""
            ## Fig. 8 — maximum load vs stream observers per moving object

            Moving objects at a {interval} ms inter-arrival time (the paper does not state its rate), `RxIntersect` observers on the first N geofences, input buffer {ExperimentContext.InputBuffer}; a probe of {rounds} updates per object passes if it drops nothing *and* its mean latency stays below the inter-arrival time (a backlogged system has not dropped yet only because the probe is finite; see `MaxLoadSearch.Passes`). Object cap {context.ObjectCap}. Maximum load (moving objects):

            {LoadTable(results, keys, "Observers / object")}
            | Paper claim (30 → 150 observers) | Paper | Measured ({observers[0]} → {observers[^1]}) |
            |---|---|---|
            | Grid | 100 → 90 | {results[(Variant.Grid, keys[0])].Display} → {results[(Variant.Grid, keys[^1])].Display} |
            | Trees | 105 → 95 | R-Tree {results[(Variant.RTree, keys[0])].Display} → {results[(Variant.RTree, keys[^1])].Display}; RUM-Tree {results[(Variant.RumTree, keys[0])].Display} → {results[(Variant.RumTree, keys[^1])].Display} |
            | SqlSpatial | 80 → 40 | {results[(Variant.SqlSpatial, keys[0])].Display} → {results[(Variant.SqlSpatial, keys[^1])].Display} |
            """));
    }

    // One fleet per (variant, observer count). Every search after the first starts from the
    // previous answer scaled by the capacity model (objects ∝ interval ÷ observers), less 20%.
    private static Dictionary<(Variant, double), MaxLoadResult> Sweep(
        ExperimentContext context, string name, double[] keys, Func<double, int> fencesAt, Func<double, double> intervalAt, int rounds)
    {
        ExperimentContext.Log(FormattableString.Invariant($"{name}: maximum load, cap {context.ObjectCap} objects, {rounds} updates per probe"));
        Dictionary<(Variant, double), MaxLoadResult> results = [];
        List<IReadOnlyList<object>> rows = [];
        foreach (Variant variant in ExperimentContext.Variants)
        {
            Fleet? fleet = null;
            int start = 16;
            try
            {
                foreach (double key in keys)
                {
                    int fences = fencesAt(key);
                    double interval = intervalAt(key);
                    if (fleet is not null)
                    {
                        MaxLoadResult previous = results[(variant, keys[Array.IndexOf(keys, key) - 1])];
                        double scale = interval / previous.Probes[0].RequestedIntervalMs * fleet.FencesPerObject / fences;
                        start = Math.Max(16, (int)(previous.MaxObjects * scale * 0.8));
                    }

                    if (fleet?.FencesPerObject != fences)
                    {
                        fleet?.Dispose();
                        fleet = new Fleet(variant, context.Workload, fences, ExperimentContext.InputBuffer, 64);
                    }

                    MaxLoadResult result = MaxLoadSearch.Find(
                        fleet!, interval, rounds, start, context.ObjectCap, probe => ExperimentContext.Log(LatencyExperiments.Describe(variant, probe)));
                    results[(variant, key)] = result;
                    ExperimentContext.Log(FormattableString.Invariant($"  {variant.Label} {name} key={key}: max load {result.Display} ({result.Probes.Count} probes, fleet built in {fleet!.BuildTime.TotalSeconds:F1} s)"));
                    foreach (StreamResult probe in result.Probes)
                    {
                        rows.Add([variant.Label, key, fences, interval, probe.Objects, MaxLoadSearch.Outcome(probe), probe.AchievedIntervalMs,
                            probe.Attempted, probe.Dropped, probe.AverageLatencyMs, probe.MaxLatenessMs, probe.PeakQueue, result.MaxObjects, result.ReachedCap]);
                    }
                }
            }
            finally
            {
                fleet?.Dispose();
            }
        }

        context.Report.WriteCsv(name,
            ["variant", "key", "fences_per_object", "interval_ms", "probe_objects", "outcome", "achieved_interval_ms", "attempted", "dropped",
             "avg_latency_ms", "max_producer_lateness_ms", "peak_queue", "max_load", "reached_cap"],
            rows);
        return results;
    }

    private static string LoadTable(Dictionary<(Variant, double), MaxLoadResult> results, double[] keys, string keyName)
        => Report.Table(
            [keyName, .. ExperimentContext.Variants.Select(v => v.Label)],
            keys.Select(k => (IReadOnlyList<object>)[k, .. ExperimentContext.Variants.Select(v => (object)results[(v, k)].Display)]));

    private static string Increase(MaxLoadResult value, MaxLoadResult baseline)
    {
        string change = FormattableString.Invariant($"{(value.MaxObjects - baseline.MaxObjects) * 100.0 / baseline.MaxObjects:+0.0;-0.0;0}%");
        return (value.ReachedCap, baseline.ReachedCap) switch
        {
            (true, true) => $"not resolved: both {value.Display}",
            (true, false) => $"≥ {change} ({value.Display} vs {baseline.Display})",
            (false, true) => $"≤ {change} ({value.Display} vs {baseline.Display})",
            _ => $"{change} ({value.Display} vs {baseline.Display})",
        };
    }

    private static string Trend(Dictionary<(Variant, double), MaxLoadResult> results, double[] keys)
        => ExperimentContext.Variants.All(v => keys.Zip(keys.Skip(1)).All(p => results[(v, p.First)].MaxObjects <= results[(v, p.Second)].MaxObjects))
            ? "yes, every variant (non-decreasing)"
            : "not monotonic for every variant";
}
