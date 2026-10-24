// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace RxSpatial.Index;

/// <summary>
/// An R*-tree spatial index (Beckmann et al., 1990) mapping items to bounding rectangles,
/// one entry per item. Updates physically move the entry. Not thread-safe.
/// </summary>
public sealed class RStarTree<T> : ISpatialIndex<T>
    where T : notnull
{
    private readonly RStarCore<T> core;
    private readonly Dictionary<T, Rect> byItem = [];

    public RStarTree()
        : this(new RTreeOptions())
    {
    }

    public RStarTree(RTreeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        core = new RStarCore<T>(options);
    }

    public int Count => byItem.Count;

    public bool Contains(T item) => byItem.ContainsKey(item);

    public void Insert(T item, in Rect bounds)
    {
        byItem.Add(item, bounds);
        core.Insert(bounds, item, 0);
    }

    public void Update(T item, in Rect bounds)
    {
        ref Rect current = ref CollectionsMarshal.GetValueRefOrAddDefault(byItem, item, out bool exists);
        if (exists)
        {
            if (current == bounds)
                return;

            core.Update(current, item, bounds, 0);
            current = bounds;
            return;
        }

        current = bounds;
        core.Insert(bounds, item, 0);
    }

    public bool Remove(T item)
    {
        if (!byItem.Remove(item, out Rect bounds))
            return false;

        core.Remove(bounds, item);
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
        AcceptAllFilter<T> filter = default;
        core.Search(bounds, results, ref filter);
    }

    public void Clear()
    {
        byItem.Clear();
        core.Clear();
    }
}
