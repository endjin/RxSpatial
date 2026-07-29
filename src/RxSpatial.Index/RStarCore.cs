using System.Buffers;

namespace RxSpatial.Index;

/// <summary>Per-entry visibility filter applied at leaves during search and cleaning.</summary>
internal interface ILeafFilter<T>
    where T : notnull
{
    bool IsLive(T item, long stamp);
}

/// <summary>A leaf filter that is also notified when an obsolete entry is physically purged.</summary>
internal interface ICleaningPolicy<T> : ILeafFilter<T>
    where T : notnull
{
    void OnPurged(T item, long stamp);
}

/// <summary>Accepts every entry; the JIT erases it from the search loop.</summary>
internal readonly struct AcceptAllFilter<T> : ILeafFilter<T>
    where T : notnull
{
    public bool IsLive(T item, long stamp) => true;
}

/// <summary>
/// The shared R*-tree engine (Beckmann, Kriegel, Schneider, Seeger 1990): ChooseSubtree by
/// minimum overlap enlargement at the leaf-parent level, forced reinsertion then topological
/// split on overflow, condense-and-reinsert on underflow. <see cref="RStarTree{T}"/> and
/// <see cref="RumTree{T}"/> are thin facades over this engine; behavioural variation enters
/// through struct-generic leaf filters, never through virtual dispatch.
/// Not thread-safe.
/// </summary>
internal sealed class RStarCore<T>
    where T : notnull
{
    private readonly int maxEntries;
    private readonly int minEntries;
    private readonly int reinsertCount;
    private readonly int capacity;
    private readonly EqualityComparer<T> comparer = EqualityComparer<T>.Default;

    // Reusable scratch for split evaluation/distribution (never live across a nested overflow).
    private readonly double[] sortKeys;
    private readonly int[] sortOrder;
    private readonly Rect[] tmpBounds;
    private readonly Rect[] prefixCover;
    private readonly Rect[] suffixCover;
    private readonly T[] tmpItems;
    private readonly long[] tmpStamps;
    private readonly Node<T>?[] tmpChildren;
    private readonly List<Node<T>> orphans = [];

    private Node<T> root = null!;
    private Node<T>? cleaningToken;
    private int reinsertedLevelsMask;

    internal RStarCore(RTreeOptions options)
    {
        maxEntries = options.MaxEntries;
        minEntries = options.MinEntries;
        reinsertCount = options.ReinsertCount;
        capacity = maxEntries + 1;
        sortKeys = new double[capacity];
        sortOrder = new int[capacity];
        tmpBounds = new Rect[capacity];
        tmpItems = new T[capacity];
        tmpStamps = new long[capacity];
        tmpChildren = new Node<T>?[capacity];
        prefixCover = new Rect[capacity + 1];
        suffixCover = new Rect[capacity + 1];
        Clear();
    }

    internal void Clear()
    {
        root = new Node<T>(capacity, 0);
        root.NextLeaf = root;
        root.PrevLeaf = root;
        cleaningToken = root;
    }

    internal void Insert(in Rect bounds, T item, long stamp)
    {
        reinsertedLevelsMask = 0;
        InsertEntry(bounds, item, stamp);
    }

    internal bool Remove(in Rect bounds, T item)
    {
        reinsertedLevelsMask = 0;
        Node<T>? leaf = FindLeaf(root, bounds, item, out int slot);
        if (leaf is null)
            return false;

        leaf.RemoveAt(slot);
        CondenseFrom(leaf);
        return true;
    }

    internal void Search<TFilter>(in Rect query, List<T> results, ref TFilter filter)
        where TFilter : struct, ILeafFilter<T>
        => SearchNode(root, query, results, ref filter);

    /// <summary>
    /// Advances the cleaning token one leaf around the ring and physically purges every entry
    /// the policy reports as not live, condensing the leaf if it underflows.
    /// </summary>
    internal void CleanNextLeaf<TPolicy>(ref TPolicy policy)
        where TPolicy : struct, ICleaningPolicy<T>
    {
        Node<T>? leaf = cleaningToken;
        if (leaf is null)
            return;

        // Pre-advance so eliminating this leaf during condense cannot dangle the token.
        cleaningToken = leaf.NextLeaf;

        bool purged = false;
        for (int i = leaf.Count - 1; i >= 0; i--)
        {
            if (!policy.IsLive(leaf.Items![i], leaf.Stamps![i]))
            {
                policy.OnPurged(leaf.Items[i], leaf.Stamps[i]);
                leaf.RemoveAt(i);
                purged = true;
            }
        }

        if (!purged)
            return;

        reinsertedLevelsMask = 0;
        if (leaf == root || leaf.Count >= minEntries)
            RefreshUpward(leaf);
        else
            CondenseFrom(leaf);
    }

    private static void SearchNode<TFilter>(Node<T> node, in Rect query, List<T> results, ref TFilter filter)
        where TFilter : struct, ILeafFilter<T>
    {
        Rect[] bounds = node.Bounds;
        int count = node.Count;
        if (node.IsLeaf)
        {
            T[] items = node.Items!;
            long[] stamps = node.Stamps!;
            for (int i = 0; i < count; i++)
            {
                if (bounds[i].Intersects(query) && filter.IsLive(items[i], stamps[i]))
                    results.Add(items[i]);
            }
        }
        else
        {
            Node<T>?[] children = node.Children!;
            for (int i = 0; i < count; i++)
            {
                if (bounds[i].Intersects(query))
                    SearchNode(children[i]!, query, results, ref filter);
            }
        }
    }

    private void InsertEntry(in Rect bounds, T item, long stamp)
    {
        if (!root.IsLeaf && root.Count == 0)
            Clear(); // transient state while condense reinserts after eliminating every leaf

        Node<T> leaf = ChooseNodeForInsert(bounds, targetLevel: 0);
        leaf.AddLeafEntry(bounds, item, stamp);
        if (leaf.Count > maxEntries)
            HandleOverflow(leaf);
    }

    /// <summary>
    /// Reattaches a detached subtree at its natural level. Invariant: the caller has taken the
    /// subtree's leaves out of the cleaning ring (<see cref="UnlinkLeavesOf"/>); they are
    /// spliced back in here once the subtree is part of the tree again.
    /// </summary>
    private void InsertSubtree(Node<T> subtree, in Rect cover)
    {
        if (!root.IsLeaf && root.Count == 0)
        {
            subtree.Parent = null;
            root = subtree;
            RelinkLeavesOf(subtree);
            return;
        }

        if (subtree.Level >= root.Level)
        {
            GrowRootWith(subtree, cover);
            RelinkLeavesOf(subtree);
            return;
        }

        Node<T> target = ChooseNodeForInsert(cover, subtree.Level + 1);
        target.AddChild(cover, subtree);
        RelinkLeavesOf(subtree);
        if (target.Count > maxEntries)
            HandleOverflow(target);
    }

    private void GrowRootWith(Node<T> subtree, in Rect cover)
    {
        var newRoot = new Node<T>(capacity, Math.Max(root.Level, subtree.Level) + 1);
        newRoot.AddChild(root.ComputeCover(), root);
        newRoot.AddChild(cover, subtree);
        root = newRoot;
    }

    /// <summary>Descends to the node at <paramref name="targetLevel"/>, enlarging covers on the way.</summary>
    private Node<T> ChooseNodeForInsert(in Rect bounds, int targetLevel)
    {
        Node<T> node = root;
        while (node.Level > targetLevel)
        {
            int best = node.Level == 1
                ? ChooseChildByOverlap(node, bounds)
                : ChooseChildByArea(node, bounds);
            node.Bounds[best] = node.Bounds[best].Union(bounds);
            node = node.Children![best]!;
        }

        return node;
    }

    /// <summary>R* leaf-parent criterion: minimum overlap enlargement, then area enlargement, then area.</summary>
    private static int ChooseChildByOverlap(Node<T> node, in Rect bounds)
    {
        Rect[] covers = node.Bounds;
        int count = node.Count;

        // Fast path: a child that already contains the rect has zero overlap enlargement and
        // zero area enlargement, so the full criterion provably selects the smallest such
        // child — no need for the O(count²) overlap computation. This is the common case for
        // update workloads, where leaf covers are broad relative to the moving objects.
        {
            int containing = -1;
            double containingArea = double.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (covers[i].Contains(bounds) && covers[i].Area < containingArea)
                {
                    containing = i;
                    containingArea = covers[i].Area;
                }
            }

            if (containing >= 0)
                return containing;
        }

        int best = 0;
        double bestOverlapDelta = double.PositiveInfinity;
        double bestEnlargement = double.PositiveInfinity;
        double bestArea = double.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Rect enlarged = covers[i].Union(bounds);
            double overlapDelta = 0;
            for (int j = 0; j < count; j++)
            {
                if (j == i)
                    continue;
                overlapDelta += enlarged.IntersectionArea(covers[j]) - covers[i].IntersectionArea(covers[j]);
            }

            double enlargement = enlarged.Area - covers[i].Area;
            double area = covers[i].Area;
            if (overlapDelta < bestOverlapDelta
                || (overlapDelta == bestOverlapDelta
                    && (enlargement < bestEnlargement
                        || (enlargement == bestEnlargement && area < bestArea))))
            {
                best = i;
                bestOverlapDelta = overlapDelta;
                bestEnlargement = enlargement;
                bestArea = area;
            }
        }

        return best;
    }

    /// <summary>Higher-level criterion: minimum area enlargement, ties by smaller area.</summary>
    private static int ChooseChildByArea(Node<T> node, in Rect bounds)
    {
        Rect[] covers = node.Bounds;
        int count = node.Count;
        int best = 0;
        double bestEnlargement = double.PositiveInfinity;
        double bestArea = double.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            double enlargement = covers[i].Enlargement(bounds);
            double area = covers[i].Area;
            if (enlargement < bestEnlargement || (enlargement == bestEnlargement && area < bestArea))
            {
                best = i;
                bestEnlargement = enlargement;
                bestArea = area;
            }
        }

        return best;
    }

    private void HandleOverflow(Node<T> node)
    {
        // R* overflow treatment: forced reinsertion once per level per top-level operation,
        // split otherwise. The mask bounds the reinsertion recursion.
        int levelBit = 1 << Math.Min(node.Level, 30);
        if (node != root && reinsertCount > 0 && (reinsertedLevelsMask & levelBit) == 0)
        {
            reinsertedLevelsMask |= levelBit;
            ForcedReinsert(node);
        }
        else
        {
            Split(node);
        }
    }

    private void ForcedReinsert(Node<T> node)
    {
        int count = node.Count;
        Rect cover = node.ComputeCover();

        // Pooled stash: reinsertion can recurse into another ForcedReinsert at a different
        // level, so the shared split scratch must not be used across these inserts. Renting
        // keeps the recursion safety of per-call arrays without their steady allocations
        // (the heap dump showed these six arrays dominating allocation counts).
        double[] keys = ArrayPool<double>.Shared.Rent(count);
        int[] order = ArrayPool<int>.Shared.Rent(count);
        int p = reinsertCount;
        Rect[] stashBounds = ArrayPool<Rect>.Shared.Rent(p);
        T[]? stashItems = node.IsLeaf ? ArrayPool<T>.Shared.Rent(p) : null;
        long[]? stashStamps = node.IsLeaf ? ArrayPool<long>.Shared.Rent(p) : null;
        Node<T>[]? stashChildren = node.IsLeaf ? null : ArrayPool<Node<T>>.Shared.Rent(p);
        try
        {
            for (int i = 0; i < count; i++)
            {
                keys[i] = node.Bounds[i].CenterDistanceSquaredTo(cover);
                order[i] = i;
            }

            Array.Sort(keys, order, 0, count);

            // Stash the p farthest entries, closest-first (the R* "close reinsert" order).
            for (int j = 0; j < p; j++)
            {
                int src = order[count - p + j];
                stashBounds[j] = node.Bounds[src];
                if (node.IsLeaf)
                {
                    stashItems![j] = node.Items![src];
                    stashStamps![j] = node.Stamps![src];
                }
                else
                {
                    stashChildren![j] = node.Children![src]!;
                    UnlinkLeavesOf(stashChildren[j]); // InsertSubtree invariant: detached leaves leave the ring
                }
            }

            // Remove the stashed slots highest-index-first so swap-removal cannot disturb
            // the remaining target indices; reuse the tail of `order` as the index buffer.
            Array.Sort(order, count - p, p);
            for (int j = count - 1; j >= count - p; j--)
                node.RemoveAt(order[j]);

            RefreshUpward(node);

            for (int j = 0; j < p; j++)
            {
                if (node.IsLeaf)
                    InsertEntry(stashBounds[j], stashItems![j], stashStamps![j]);
                else
                    InsertSubtree(stashChildren![j], stashBounds[j]);
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(keys);
            ArrayPool<int>.Shared.Return(order);
            ArrayPool<Rect>.Shared.Return(stashBounds);
            if (stashItems is not null)
                ArrayPool<T>.Shared.Return(stashItems, clearArray: true);
            if (stashStamps is not null)
                ArrayPool<long>.Shared.Return(stashStamps);
            if (stashChildren is not null)
                ArrayPool<Node<T>>.Shared.Return(stashChildren, clearArray: true);
        }
    }

    private void Split(Node<T> node)
    {
        int count = node.Count;
        (int axis, bool byMax, int splitAt) = ChooseSplit(node, count);

        FillSortKeys(node, count, axis, byMax);
        Array.Sort(sortKeys, sortOrder, 0, count);

        for (int i = 0; i < count; i++)
        {
            int src = sortOrder[i];
            tmpBounds[i] = node.Bounds[src];
            if (node.IsLeaf)
            {
                tmpItems[i] = node.Items![src];
                tmpStamps[i] = node.Stamps![src];
            }
            else
            {
                tmpChildren[i] = node.Children![src];
            }
        }

        var sibling = new Node<T>(capacity, node.Level);
        node.Count = 0;
        for (int i = 0; i < splitAt; i++)
        {
            if (node.IsLeaf)
                node.AddLeafEntry(tmpBounds[i], tmpItems[i], tmpStamps[i]);
            else
                node.AddChild(tmpBounds[i], tmpChildren[i]!);
        }

        node.ClearFrom(splitAt);
        for (int i = splitAt; i < count; i++)
        {
            if (node.IsLeaf)
                sibling.AddLeafEntry(tmpBounds[i], tmpItems[i], tmpStamps[i]);
            else
                sibling.AddChild(tmpBounds[i], tmpChildren[i]!);
        }

        Array.Clear(tmpItems);
        Array.Clear(tmpChildren);

        if (node.IsLeaf)
            LinkLeafAfter(node, sibling);

        if (node == root)
        {
            var newRoot = new Node<T>(capacity, node.Level + 1);
            newRoot.AddChild(node.ComputeCover(), node);
            newRoot.AddChild(sibling.ComputeCover(), sibling);
            root = newRoot;
        }
        else
        {
            Node<T> parent = node.Parent!;
            parent.Bounds[parent.IndexOfChild(node)] = node.ComputeCover();
            parent.AddChild(sibling.ComputeCover(), sibling);
            if (parent.Count > maxEntries)
                HandleOverflow(parent);
        }
    }

    /// <summary>
    /// R* topological split: axis by least margin sum over all distributions of both sortings,
    /// split index on that axis by least intersection area, ties by least combined area.
    /// </summary>
    private (int Axis, bool ByMax, int SplitAt) ChooseSplit(Node<T> node, int count)
    {
        double bestAxisMargin = double.PositiveInfinity;
        int bestAxis = 0;
        (bool ByMax, int SplitAt) bestOnAxis = default;

        for (int axis = 0; axis < 2; axis++)
        {
            double marginSum = 0;
            double bestOverlap = double.PositiveInfinity;
            double bestAreaSum = double.PositiveInfinity;
            (bool ByMax, int SplitAt) candidate = default;

            for (int sorting = 0; sorting < 2; sorting++)
            {
                bool byMax = sorting == 1;
                FillSortKeys(node, count, axis, byMax);
                Array.Sort(sortKeys, sortOrder, 0, count);

                prefixCover[1] = node.Bounds[sortOrder[0]];
                for (int i = 1; i < count; i++)
                    prefixCover[i + 1] = prefixCover[i].Union(node.Bounds[sortOrder[i]]);
                suffixCover[count - 1] = node.Bounds[sortOrder[count - 1]];
                for (int i = count - 2; i >= 0; i--)
                    suffixCover[i] = suffixCover[i + 1].Union(node.Bounds[sortOrder[i]]);

                for (int splitAt = minEntries; splitAt <= count - minEntries; splitAt++)
                {
                    Rect group1 = prefixCover[splitAt];
                    Rect group2 = suffixCover[splitAt];
                    marginSum += group1.Margin + group2.Margin;

                    double overlap = group1.IntersectionArea(group2);
                    double areaSum = group1.Area + group2.Area;
                    if (overlap < bestOverlap || (overlap == bestOverlap && areaSum < bestAreaSum))
                    {
                        bestOverlap = overlap;
                        bestAreaSum = areaSum;
                        candidate = (byMax, splitAt);
                    }
                }
            }

            if (marginSum < bestAxisMargin)
            {
                bestAxisMargin = marginSum;
                bestAxis = axis;
                bestOnAxis = candidate;
            }
        }

        return (bestAxis, bestOnAxis.ByMax, bestOnAxis.SplitAt);
    }

    private void FillSortKeys(Node<T> node, int count, int axis, bool byMax)
    {
        Rect[] bounds = node.Bounds;
        for (int i = 0; i < count; i++)
        {
            sortKeys[i] = axis == 0
                ? (byMax ? bounds[i].MaxX : bounds[i].MinX)
                : (byMax ? bounds[i].MaxY : bounds[i].MinY);
            sortOrder[i] = i;
        }
    }

    private Node<T>? FindLeaf(Node<T> node, in Rect bounds, T item, out int slot)
    {
        if (node.IsLeaf)
        {
            for (int i = 0; i < node.Count; i++)
            {
                if (node.Bounds[i] == bounds && comparer.Equals(node.Items![i], item))
                {
                    slot = i;
                    return node;
                }
            }
        }
        else
        {
            for (int i = 0; i < node.Count; i++)
            {
                if (node.Bounds[i].Contains(bounds))
                {
                    Node<T>? found = FindLeaf(node.Children![i]!, bounds, item, out slot);
                    if (found is not null)
                        return found;
                }
            }
        }

        slot = -1;
        return null;
    }

    /// <summary>
    /// R-tree condensation from a shrunken node: eliminate underfull ancestors, tighten covers,
    /// reinsert orphaned entries at their original levels, then shrink the root.
    /// </summary>
    private void CondenseFrom(Node<T> node)
    {
        orphans.Clear();
        Node<T> current = node;
        while (current.Parent is not null)
        {
            Node<T> parent = current.Parent;
            int slot = parent.IndexOfChild(current);
            if (current.Count < minEntries)
            {
                parent.RemoveAt(slot);
                current.Parent = null;
                UnlinkLeavesOf(current);
                orphans.Add(current);
            }
            else
            {
                parent.Bounds[slot] = current.ComputeCover();
            }

            current = parent;
        }

        for (int i = 0; i < orphans.Count; i++)
        {
            Node<T> orphan = orphans[i];
            for (int j = 0; j < orphan.Count; j++)
            {
                if (orphan.IsLeaf)
                    InsertEntry(orphan.Bounds[j], orphan.Items![j], orphan.Stamps![j]);
                else
                    InsertSubtree(orphan.Children![j]!, orphan.Bounds[j]);
            }
        }

        orphans.Clear();
        ShrinkRoot();
    }

    private void ShrinkRoot()
    {
        while (!root.IsLeaf)
        {
            if (root.Count == 1)
            {
                root = root.Children![0]!;
                root.Parent = null;
            }
            else if (root.Count == 0)
            {
                Clear();
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>Recomputes ancestor covers from <paramref name="node"/> upward until unchanged.</summary>
    private static void RefreshUpward(Node<T> node)
    {
        Node<T> current = node;
        while (current.Parent is not null)
        {
            Node<T> parent = current.Parent;
            int slot = parent.IndexOfChild(current);
            Rect cover = current.ComputeCover();
            if (parent.Bounds[slot] == cover)
                return;

            parent.Bounds[slot] = cover;
            current = parent;
        }
    }

    private static void LinkLeafAfter(Node<T> existing, Node<T> newLeaf)
    {
        Node<T> next = existing.NextLeaf!;
        existing.NextLeaf = newLeaf;
        newLeaf.PrevLeaf = existing;
        newLeaf.NextLeaf = next;
        next.PrevLeaf = newLeaf;
    }

    private void UnlinkLeaf(Node<T> leaf)
    {
        if (leaf.NextLeaf is null)
            return; // not in the ring

        if (cleaningToken == leaf)
            cleaningToken = leaf.NextLeaf == leaf ? null : leaf.NextLeaf;

        Node<T> prev = leaf.PrevLeaf!;
        Node<T> next = leaf.NextLeaf!;
        prev.NextLeaf = next;
        next.PrevLeaf = prev;
        leaf.NextLeaf = null;
        leaf.PrevLeaf = null;
    }

    /// <summary>Takes every leaf of a detached subtree out of the cleaning ring.</summary>
    private void UnlinkLeavesOf(Node<T> node)
    {
        if (node.IsLeaf)
        {
            UnlinkLeaf(node);
            return;
        }

        for (int i = 0; i < node.Count; i++)
            UnlinkLeavesOf(node.Children![i]!);
    }

    /// <summary>Splices every leaf of a reattached subtree back into the cleaning ring.</summary>
    private void RelinkLeavesOf(Node<T> node)
    {
        if (node.IsLeaf)
        {
            if (cleaningToken is null)
            {
                node.NextLeaf = node;
                node.PrevLeaf = node;
                cleaningToken = node;
            }
            else
            {
                LinkLeafAfter(cleaningToken, node);
            }

            return;
        }

        for (int i = 0; i < node.Count; i++)
            RelinkLeavesOf(node.Children![i]!);
    }
}
