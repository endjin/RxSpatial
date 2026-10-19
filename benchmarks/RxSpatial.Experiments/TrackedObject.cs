using Microsoft.SqlServer.Types;
using RxSpatial.SqlServer;

namespace RxSpatial.Experiments;

/// <summary>
/// One stream source: an <see cref="RxSqlGeography"/> with an <c>RxIntersect</c> per geofence,
/// the route it replays, and the producer-side counters of accepted and dropped pushes. A push
/// is dropped when the moving object's input buffer is full and <see cref="RxSqlGeography.OnNext"/>
/// throws <see cref="RxSpatialOverloadException"/> — the paper's load shedding.
/// </summary>
internal sealed class TrackedObject : IDisposable
{
    private readonly SqlGeography[] route;
    private readonly List<IDisposable> subscriptions;
    private readonly QueryCounter counter = new();
    private int cursor;

    public TrackedObject(RxSpatialOptions options, IReadOnlyList<SqlGeography> fences, int fenceCount, SqlGeography[] route)
    {
        this.route = route;
        Geography = new RxSqlGeography(options);
        subscriptions = new List<IDisposable>(fenceCount);
        for (int i = 0; i < fenceCount; i++)
            subscriptions.Add(Geography.RxIntersect(fences[i], counter));
    }

    public RxSqlGeography Geography { get; }

    /// <summary>Pushes accepted into the input buffer (producer thread only).</summary>
    public long Accepted { get; private set; }

    /// <summary>Pushes rejected with <see cref="RxSpatialOverloadException"/> (producer thread only).</summary>
    public long Dropped { get; private set; }

    /// <summary>Deepest input buffer seen just before a push since <see cref="ResetPeakQueue"/>.</summary>
    public int PeakQueue { get; private set; }

    /// <summary>Query evaluations performed so far: observers notified across all updates.</summary>
    public long Evaluations => counter.Evaluations;

    /// <summary>Evaluations whose answer was "inside the fence".</summary>
    public long Matches => counter.Matches;

    public void ResetPeakQueue() => PeakQueue = 0;

    public bool TryPush()
    {
        PeakQueue = Math.Max(PeakQueue, Geography.QueuedPushCount);
        SqlGeography location = route[cursor];
        cursor = (cursor + 1) % route.Length;
        try
        {
            Geography.OnNext(location);
            Accepted++;
            return true;
        }
        catch (RxSpatialOverloadException)
        {
            Dropped++;
            return false;
        }
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in subscriptions)
            subscription.Dispose();
    }

    // The query results sink. One drainer at a time runs per moving object, but bursts can
    // land on different pool threads, so the counts use interlocked increments.
    private sealed class QueryCounter : IObserver<bool>
    {
        private long evaluations;
        private long matches;

        public long Evaluations => Volatile.Read(ref evaluations);

        public long Matches => Volatile.Read(ref matches);

        public void OnNext(bool value)
        {
            Interlocked.Increment(ref evaluations);
            if (value)
                Interlocked.Increment(ref matches);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
