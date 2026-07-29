using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RxSpatial.Index;

/// <summary>
/// A RUM-tree — R-tree with Update Memo (Xiong &amp; Aref) — layered on the shared R*-tree core.
/// <see cref="Update"/> only inserts a freshly stamped entry; the superseded entry stays in
/// place and is filtered out of searches by the memo until a round-robin cleaning token purges
/// it, so frequent movers never pay for top-down deletion. <see cref="Remove"/> writes a memo
/// tombstone without touching the tree. Not thread-safe.
/// </summary>
public sealed class RumTree<T> : ISpatialIndex<T>
    where T : notnull
{
    private readonly RStarCore<T> core;
    private readonly Dictionary<T, MemoEntry> memo = [];
    private readonly int cleaningInterval;
    private long stampCounter;
    private int liveCount;
    private int pendingObsolete;
    private int opsSinceCleaning;

    public RumTree()
        : this(new RumTreeOptions())
    {
    }

    public RumTree(RumTreeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        cleaningInterval = options.CleaningInterval;
        core = new RStarCore<T>(options);
    }

    public int Count => liveCount;

    /// <summary>Obsolete physical entries awaiting the cleaning token.</summary>
    public int PendingObsoleteCount => pendingObsolete;

    public bool Contains(T item) => memo.TryGetValue(item, out MemoEntry entry) && entry.IsLive;

    public void Insert(T item, in Rect bounds)
    {
        ref MemoEntry entry = ref CollectionsMarshal.GetValueRefOrAddDefault(memo, item, out bool exists);
        if (exists && entry.IsLive)
            throw new ArgumentException("An item with the same key has already been added.", nameof(item));

        entry.LatestStamp = ++stampCounter;
        entry.IsLive = true;
        liveCount++;
        core.Insert(bounds, item, entry.LatestStamp);
    }

    public void Update(T item, in Rect bounds)
    {
        long stamp = ++stampCounter;
        bool createdGarbage;

        // Scoped so the memo reference cannot outlive the block: core.Insert below may run
        // the cleaning token, which removes retired entries and invalidates the reference.
        {
            ref MemoEntry entry = ref CollectionsMarshal.GetValueRefOrAddDefault(memo, item, out bool exists);
            createdGarbage = exists && entry.IsLive;
            if (createdGarbage)
            {
                entry.ObsoleteCount++;
                pendingObsolete++;
            }
            else
            {
                entry.IsLive = true;
                liveCount++;
            }

            entry.LatestStamp = stamp;
        }

        core.Insert(bounds, item, stamp);
        if (createdGarbage)
            MaybeClean();
    }

    public bool Remove(T item)
    {
        ref MemoEntry entry = ref CollectionsMarshal.GetValueRefOrNullRef(memo, item);
        if (Unsafe.IsNullRef(ref entry) || !entry.IsLive)
            return false;

        entry.IsLive = false;
        entry.ObsoleteCount++;
        pendingObsolete++;
        liveCount--;
        MaybeClean();
        return true;
    }

    public List<T> Search(in Rect bounds)
    {
        List<T> results = [];
        Search(bounds, results);
        return results;
    }

    public void Search(in Rect bounds, List<T> results)
    {
        MemoFilter filter = new(memo);
        core.Search(bounds, results, ref filter);
    }

    public void Clear()
    {
        core.Clear();
        memo.Clear();
        stampCounter = 0;
        liveCount = 0;
        pendingObsolete = 0;
        opsSinceCleaning = 0;
    }

    private void MaybeClean()
    {
        if (++opsSinceCleaning < cleaningInterval)
            return;

        opsSinceCleaning = 0;
        CleaningPolicy policy = new(this);
        core.CleanNextLeaf(ref policy);
    }

    private void OnEntryPurged(T item)
    {
        ref MemoEntry entry = ref CollectionsMarshal.GetValueRefOrNullRef(memo, item);
        entry.ObsoleteCount--;
        pendingObsolete--;
        if (!entry.IsLive && entry.ObsoleteCount == 0)
            memo.Remove(item);
    }

    private struct MemoEntry
    {
        public long LatestStamp;
        public int ObsoleteCount;
        public bool IsLive;
    }

    private readonly struct MemoFilter(Dictionary<T, MemoEntry> memo) : ILeafFilter<T>
    {
        public bool IsLive(T item, long stamp)
        {
            ref MemoEntry entry = ref CollectionsMarshal.GetValueRefOrNullRef(memo, item);
            return !Unsafe.IsNullRef(ref entry) && entry.IsLive && entry.LatestStamp == stamp;
        }
    }

    private readonly struct CleaningPolicy(RumTree<T> owner) : ICleaningPolicy<T>
    {
        public bool IsLive(T item, long stamp)
        {
            ref MemoEntry entry = ref CollectionsMarshal.GetValueRefOrNullRef(owner.memo, item);
            return !Unsafe.IsNullRef(ref entry) && entry.IsLive && entry.LatestStamp == stamp;
        }

        public void OnPurged(T item, long stamp) => owner.OnEntryPurged(item);
    }
}
