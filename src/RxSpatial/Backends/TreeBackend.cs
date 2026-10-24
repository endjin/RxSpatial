// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using System.Globalization;
using RxSpatial.Index;

namespace RxSpatial.Backends;

/// <summary>
/// Tree backend: observers are indexed by the bounding box of their shape in either the
/// R*-tree or the RUM-tree (R-tree with Update Memo) from RxSpatial.Index.
/// </summary>
internal sealed class TreeBackend<TShape, TOps, T> : ISpatialBackend<TShape, T>
    where TShape : class
    where TOps : struct, ISpatialOperations<TShape>
    where T : notnull
{
    private readonly ISpatialIndex<T> index;
    private readonly Dictionary<T, IDisposable> trackers = [];

    public TreeBackend(RxGeographyDataStruct treeType, RxSpatialOptions options)
        => index = treeType == RxGeographyDataStruct.RumTree
            ? new RumTree<T>(new RumTreeOptions { CleaningInterval = options.RumTreeCleaningInterval })
            : new RStarTree<T>();

    public void Subscribe(T observer, IMovingObject<TShape> source)
    {
        // Preserved behaviour: source.Location is null until the moving object's first OnNext
        // push; subscribing before it has reported a location fails, as it always has.
        index.Update(observer, TOps.BoundsOf(source.Location!));

        // The tracker handle is kept so Remove can end it: otherwise the tracked object's next
        // push would re-index (Update is an upsert) the observer that has just been unsubscribed.
        trackers[observer] = source.Subscribe(new LocationTracker(index, observer));
    }

    public void Subscribe(T observer, TShape location) => index.Update(observer, TOps.BoundsOf(location));

    public List<T> GetObserversInZone(TShape location) => index.Search(TOps.BoundsOf(location));

    public bool Remove(T observer)
    {
        if (trackers.Remove(observer, out IDisposable? tracker))
            tracker.Dispose();

        return index.Remove(observer);
    }

    public bool Contains(T observer) => index.Contains(observer);

    /// <summary>Number of indexed observers — diagnostic snapshot.</summary>
    public override string ToString() => index.Count.ToString(CultureInfo.InvariantCulture);

    // Tracks a moving observer and updates its indexed position on every location push.
    private sealed class LocationTracker(ISpatialIndex<T> index, T observer) : IObserver<TShape>
    {
        public void OnNext(TShape location) => index.Update(observer, TOps.BoundsOf(location));

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
