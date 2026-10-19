namespace RxSpatial.Experiments;

/// <summary>The outcome of a maximum-load search.</summary>
/// <param name="MaxObjects">The most moving objects sustained (see <see cref="MaxLoadSearch.Passes"/>).</param>
/// <param name="ReachedCap">The search stopped at the object cap without a failing probe.</param>
/// <param name="FirstFailing">The smallest probed object count that failed, if any.</param>
/// <param name="Probes">Every probe run, in order.</param>
internal sealed record MaxLoadResult(int MaxObjects, bool ReachedCap, int? FirstFailing, IReadOnlyList<StreamResult> Probes)
{
    /// <summary>The value for a table cell: "≥cap" when the cap, not the system, ended the search.</summary>
    public string Display => ReachedCap
        ? FormattableString.Invariant($"≥{MaxObjects}")
        : MaxObjects.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// The paper's "maximum load": the largest number of moving objects that can be tracked
/// without dropping an incoming location update. Doubles the object count until a probe fails
/// (or the cap is reached), then bisects to within 10%.
/// </summary>
internal static class MaxLoadSearch
{
    public static MaxLoadResult Find(Fleet fleet, double intervalMs, int updatesPerObject, int start, int cap, Action<StreamResult>? progress = null)
    {
        List<StreamResult> probes = [];
        int good = 0;
        int? bad = null;

        int n = Math.Clamp(start, 1, cap);
        while (true)
        {
            if (Probe(n))
            {
                good = n;
                if (n >= cap)
                    break;

                n = Math.Min(cap, n * 2);
            }
            else
            {
                bad = n;
                break;
            }
        }

        while (bad is int failing && failing - good > Math.Max(1, good / 10))
        {
            int middle = good + ((failing - good) / 2);
            if (Probe(middle))
                good = middle;
            else
                bad = middle;
        }

        return new MaxLoadResult(good, bad is null, bad, probes);

        bool Probe(int count)
        {
            StreamResult result = StreamRunner.Run(fleet.Take(count), intervalMs, updatesPerObject, stopOnFirstDrop: true);
            probes.Add(result);
            progress?.Invoke(result);
            return Passes(result);
        }
    }

    /// <summary>
    /// A probe passes if it dropped nothing and was not backlogged. A finite probe can be
    /// overloaded without dropping — an object's input buffer grows by at most one update per
    /// round, and a 20-round probe at twice the sustainable load still fits a buffer of 10 —
    /// but then updates wait behind each other and the mean latency exceeds the inter-arrival
    /// time. Such a system drops on any longer run, so it does not count as sustaining the load.
    /// </summary>
    public static bool Passes(StreamResult result)
        => result.Dropped == 0 && result.AverageLatencyMs <= result.RequestedIntervalMs; // NaN (never drained) fails

    public static string Outcome(StreamResult result)
        => result.Dropped > 0 ? "drop" : Passes(result) ? "pass" : "backlog";
}
