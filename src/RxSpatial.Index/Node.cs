namespace RxSpatial.Index;

/// <summary>
/// A tree node with parallel entry arrays sized MaxEntries + 1, so an overflowing entry can be
/// placed before overflow treatment runs. A child's cover rectangle lives only in its parent's
/// <see cref="Bounds"/> slot (single source of truth — nodes cache no own MBR). Leaves form a
/// circular ring (<see cref="NextLeaf"/>/<see cref="PrevLeaf"/>) for the RUM cleaning token.
/// </summary>
internal sealed class Node<T>
    where T : notnull
{
    internal readonly Rect[] Bounds;
    internal readonly Node<T>?[]? Children; // internal nodes only
    internal readonly T[]? Items;           // leaves only
    internal readonly long[]? Stamps;       // leaves only
    internal readonly int Level;            // 0 = leaf
    internal int Count;
    internal Node<T>? Parent;
    internal Node<T>? NextLeaf;
    internal Node<T>? PrevLeaf;

    internal Node(int capacity, int level)
    {
        Level = level;
        Bounds = new Rect[capacity];
        if (level == 0)
        {
            Items = new T[capacity];
            Stamps = new long[capacity];
        }
        else
        {
            Children = new Node<T>?[capacity];
        }
    }

    internal bool IsLeaf => Level == 0;

    internal void AddLeafEntry(in Rect bounds, T item, long stamp)
    {
        Bounds[Count] = bounds;
        Items![Count] = item;
        Stamps![Count] = stamp;
        Count++;
    }

    internal void AddChild(in Rect cover, Node<T> child)
    {
        Bounds[Count] = cover;
        Children![Count] = child;
        Count++;
        child.Parent = this;
    }

    /// <summary>Swap-with-last removal; clears the vacated slot so no stale references linger.</summary>
    internal void RemoveAt(int index)
    {
        int last = Count - 1;
        Bounds[index] = Bounds[last];
        if (IsLeaf)
        {
            Items![index] = Items[last];
            Stamps![index] = Stamps[last];
            Items[last] = default!;
        }
        else
        {
            Children![index] = Children[last];
            Children[last] = null;
        }

        Count = last;
    }

    /// <summary>Clears entry slots from <paramref name="from"/> onward (post-split hygiene).</summary>
    internal void ClearFrom(int from)
    {
        if (IsLeaf)
        {
            Array.Clear(Items!, from, Items!.Length - from);
        }
        else
        {
            Array.Clear(Children!, from, Children!.Length - from);
        }
    }

    internal Rect ComputeCover()
    {
        Rect cover = Bounds[0];
        for (int i = 1; i < Count; i++)
            cover = cover.Union(Bounds[i]);
        return cover;
    }

    internal int IndexOfChild(Node<T> child)
    {
        Node<T>?[] children = Children!;
        for (int i = 0; i < Count; i++)
        {
            if (ReferenceEquals(children[i], child))
                return i;
        }

        throw new InvalidOperationException("Child node not found in its parent.");
    }
}
