using System.Diagnostics;

namespace RxSpatial.Experiments;

/// <summary>What one paced run of location updates measured.</summary>
/// <param name="Objects">Stream sources (moving objects) pushing.</param>
/// <param name="RequestedIntervalMs">Requested location update inter-arrival time per object.</param>
/// <param name="AchievedIntervalMs">Achieved inter-arrival time per object (elapsed × objects ÷ pushes).</param>
/// <param name="Attempted">Location updates offered to the moving objects.</param>
/// <param name="Dropped">Updates rejected with <see cref="RxSpatialOverloadException"/>.</param>
/// <param name="AverageLatencyMs">
/// Mean of the library's own latency (input buffer → result propagated) over the run's
/// accepted updates; <see cref="double.NaN"/> if the buffers did not drain.
/// </param>
/// <param name="MeanLatenessMs">How late the producer issued pushes against the schedule, on average.</param>
/// <param name="MaxLatenessMs">The worst producer lateness.</param>
/// <param name="PeakQueue">Deepest input buffer any object had just before a push.</param>
/// <param name="EvaluationsPerUpdate">Query observers evaluated per accepted update (the index's pruning).</param>
/// <param name="Matches">Evaluations answering "inside the fence" — the correctness cross-check.</param>
/// <param name="StoppedEarly">The run stopped at its first drop (maximum-load probes).</param>
internal sealed record StreamResult(
    int Objects,
    double RequestedIntervalMs,
    double AchievedIntervalMs,
    long Attempted,
    long Dropped,
    double AverageLatencyMs,
    double MeanLatenessMs,
    double MaxLatenessMs,
    int PeakQueue,
    double EvaluationsPerUpdate,
    long Matches,
    bool StoppedEarly);

/// <summary>
/// Drives moving objects at a fixed location update inter-arrival time from one dedicated,
/// high-priority producer thread. Object <c>i</c>'s <c>k</c>-th update is due at
/// <c>start + k·T + i·T/n</c>, so the n sources are evenly staggered and the aggregate rate is
/// n/T. The moving objects run in buffered mode: pushes queue in each object's input buffer
/// and a thread-pool drainer processes them, as in the paper's set-up.
/// </summary>
internal static class StreamRunner
{
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(150);

    public static StreamResult Run(IReadOnlyList<TrackedObject> objects, double intervalMs, int updatesPerObject, bool stopOnFirstDrop = false)
    {
        StreamResult? result = null;
        Thread producer = new(() => result = Produce(objects, intervalMs, updatesPerObject, stopOnFirstDrop))
        {
            Name = "Location update producer",
            Priority = ThreadPriority.Highest,
            IsBackground = true,
        };
        producer.Start();
        producer.Join();
        return result!;
    }

    private static StreamResult Produce(IReadOnlyList<TrackedObject> objects, double intervalMs, int updatesPerObject, bool stopOnFirstDrop)
    {
        int n = objects.Count;
        Snapshot before = Snapshot.Of(objects);
        foreach (TrackedObject tracked in objects)
            tracked.ResetPeakQueue();

        long period = Pacer.Ticks(intervalMs);
        long start = Stopwatch.GetTimestamp() + Pacer.Ticks(5);
        long attempted = 0;
        long dropped = 0;
        double totalLateness = 0;
        double maxLateness = 0;
        bool stoppedEarly = false;
        long last = start;

        for (int k = 0; k < updatesPerObject && !stoppedEarly; k++)
        {
            for (int i = 0; i < n; i++)
            {
                long due = start + (k * period) + (i * period / n);
                Pacer.WaitUntil(due);
                last = Stopwatch.GetTimestamp();
                double lateness = Pacer.Milliseconds(last - due);
                totalLateness += lateness;
                maxLateness = Math.Max(maxLateness, lateness);

                attempted++;
                if (!objects[i].TryPush())
                {
                    dropped++;
                    if (stopOnFirstDrop)
                    {
                        stoppedEarly = true;
                        break;
                    }
                }
            }
        }

        // The span from the first due time to the last push covers (attempted − 1) of the
        // n/T-per-second slots.
        double achievedIntervalMs = attempted > 1
            ? Pacer.Milliseconds(last - start) * n / (attempted - 1)
            : intervalMs;

        bool drained = SpinWait.SpinUntil(() => objects.All(o => o.Geography.QueuedPushCount == 0), DrainTimeout);
        Snapshot after = Settle(objects);

        long accepted = after.Accepted - before.Accepted;
        double latency = drained && accepted > 0
            ? (after.LatencyTotal - before.LatencyTotal) / accepted
            : double.NaN;

        return new StreamResult(
            n,
            intervalMs,
            achievedIntervalMs,
            attempted,
            dropped,
            latency,
            attempted > 0 ? totalLateness / attempted : 0,
            maxLateness,
            objects.Max(o => o.PeakQueue),
            accepted > 0 ? (double)(after.Evaluations - before.Evaluations) / accepted : 0,
            after.Matches - before.Matches,
            stoppedEarly);
    }

    // An empty buffer does not mean a finished drainer: each may still be processing the update
    // it dequeued last, and under a backlog dozens are, each slowed by the others. Wait until
    // the query evaluations and latency totals stop moving for three settle periods in a row.
    private static Snapshot Settle(IReadOnlyList<TrackedObject> objects)
    {
        Snapshot previous = Snapshot.Of(objects);
        for (int quiet = 0; quiet < 3;)
        {
            Thread.Sleep(SettleTime);
            Snapshot current = Snapshot.Of(objects);
            quiet = current == previous ? quiet + 1 : 0;
            previous = current;
        }

        return previous;
    }

    // Each object's AverageLatency is a running mean over every update it has processed, which
    // after a drain is every update it accepted; mean × count recovers the latency total, so the
    // difference of two snapshots is the total for the updates in between.
    private readonly record struct Snapshot(long Accepted, double LatencyTotal, long Evaluations, long Matches)
    {
        public static Snapshot Of(IReadOnlyList<TrackedObject> objects)
        {
            long accepted = 0;
            double latencyTotal = 0;
            long evaluations = 0;
            long matches = 0;
            foreach (TrackedObject tracked in objects)
            {
                accepted += tracked.Accepted;
                latencyTotal += tracked.Geography.AverageLatency * tracked.Accepted;
                evaluations += tracked.Evaluations;
                matches += tracked.Matches;
            }

            return new Snapshot(accepted, latencyTotal, evaluations, matches);
        }
    }
}
