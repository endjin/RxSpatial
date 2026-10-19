using System.Diagnostics;

namespace RxSpatial.Experiments;

/// <summary>
/// A growable set of moving objects on one variant, each subscribed to the same geofences.
/// Building subscriptions dominates set-up for the tree variants, so a load search reuses one
/// fleet across probes and inter-arrival times and only grows it.
/// </summary>
internal sealed class Fleet : IDisposable
{
    private readonly List<TrackedObject> objects = [];
    private readonly RxSpatialOptions options;
    private readonly SeattleWorkload workload;
    private readonly int fencesPerObject;
    private readonly int routeLength;

    public Fleet(Variant variant, SeattleWorkload workload, int fencesPerObject, int queueLimit, int routeLength, bool visualized = false)
    {
        Variant = variant;
        this.workload = workload;
        this.fencesPerObject = fencesPerObject;
        this.routeLength = routeLength;
        options = new RxSpatialOptions
        {
            PreferredDataStruct = variant.DataStruct,
            QueueLimit = queueLimit,
            IsVisualized = visualized,
        };
    }

    public Variant Variant { get; }

    public int FencesPerObject => fencesPerObject;

    public TimeSpan BuildTime { get; private set; }

    public IReadOnlyList<TrackedObject> Objects => objects;

    /// <summary>
    /// The first <paramref name="count"/> objects, building any that do not exist yet. Objects
    /// are built in parallel: a tree or grid subscription computes the fence's bounding box
    /// through a WKB round-trip (~0.2 ms for an <c>STBuffer</c> polygon), so a thousand
    /// objects × 2000 fences would otherwise take minutes.
    /// </summary>
    public IReadOnlyList<TrackedObject> Take(int count)
    {
        int first = objects.Count;
        if (count > first)
        {
            long start = Stopwatch.GetTimestamp();
            TrackedObject[] built = new TrackedObject[count - first];
            Parallel.For(0, built.Length, i =>
                built[i] = new TrackedObject(options, workload.Fences, fencesPerObject, workload.Route(first + i, routeLength)));
            objects.AddRange(built);
            BuildTime += Stopwatch.GetElapsedTime(start);
        }

        return objects.GetRange(0, count);
    }

    public void Dispose()
    {
        foreach (TrackedObject tracked in objects)
            tracked.Dispose();

        objects.Clear();
    }
}
