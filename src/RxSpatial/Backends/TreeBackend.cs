using System.Globalization;
using Microsoft.SqlServer.Types;
using RxSpatial.Index;

namespace RxSpatial.Backends;

/// <summary>
/// Tree backend: observers are indexed by the bounding box of their geography in either the
/// R*-tree or the RUM-tree (R-tree with Update Memo) from RxSpatial.Index.
/// </summary>
internal sealed class TreeBackend<T> : ISpatialBackend<T>
    where T : notnull
{
    private readonly ISpatialIndex<T> index;
    private readonly Dictionary<T, IDisposable> trackers = [];

    public TreeBackend(RxGeographyDataStruct treeType, RxSpatialOptions options)
        => index = treeType == RxGeographyDataStruct.RumTree
            ? new RumTree<T>(new RumTreeOptions { CleaningInterval = options.RumTreeCleaningInterval })
            : new RStarTree<T>();

    public void Subscribe(T observer, RxGeography source)
    {
        // Preserved behaviour: source.Location is null until the moving object's first OnNext
        // push; subscribing before it has reported a location fails, as it always has.
        index.Update(observer, BoundsOf(source.Location!));

        // The tracker handle is kept so Remove can end it: otherwise the tracked object's next
        // push would re-index (Update is an upsert) the observer that has just been unsubscribed.
        trackers[observer] = source.Subscribe(new LocationTracker(index, observer));
    }

    public void Subscribe(T observer, SqlGeography location) => index.Update(observer, BoundsOf(location));

    public List<T> GetObserversInZone(SqlGeography location) => index.Search(BoundsOf(location));

    public bool Remove(T observer)
    {
        if (trackers.Remove(observer, out IDisposable? tracker))
            tracker.Dispose();

        return index.Remove(observer);
    }

    public bool Contains(T observer) => index.Contains(observer);

    /// <summary>Number of indexed observers — diagnostic snapshot.</summary>
    public override string ToString() => index.Count.ToString(CultureInfo.InvariantCulture);

    private static Rect BoundsOf(SqlGeography location) => SpatialBounds.OfGeography(location);

    // Tracks a moving observer and updates its indexed position on every location push.
    private sealed class LocationTracker(ISpatialIndex<T> index, T observer) : IObserver<SqlGeography>
    {
        public void OnNext(SqlGeography location) => index.Update(observer, BoundsOf(location));

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
