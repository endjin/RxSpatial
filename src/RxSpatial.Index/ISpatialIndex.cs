namespace RxSpatial.Index;

/// <summary>
/// A spatial index mapping items to bounding rectangles, with one entry per item.
/// Implementations are not thread-safe; callers synchronise access.
/// </summary>
public interface ISpatialIndex<T>
    where T : notnull
{
    /// <summary>The number of live items in the index.</summary>
    int Count { get; }

    /// <summary>Whether <paramref name="item"/> is currently live in the index.</summary>
    bool Contains(T item);

    /// <summary>Adds a new item. Throws <see cref="ArgumentException"/> if the item is already live.</summary>
    void Insert(T item, in Rect bounds);

    /// <summary>Adds the item, or moves it to <paramref name="bounds"/> if it is already indexed.</summary>
    void Update(T item, in Rect bounds);

    /// <summary>Removes the item. Returns false (and does not throw) when the item is not live.</summary>
    bool Remove(T item);

    /// <summary>
    /// All live items whose bounds intersect <paramref name="bounds"/> (closed-interval:
    /// touching counts). Never null; empty on an empty index; contains no duplicates;
    /// result order is unspecified.
    /// </summary>
    List<T> Search(in Rect bounds);

    /// <summary>Appends matching live items to <paramref name="results"/> (buffer-reuse overload).</summary>
    void Search(in Rect bounds, List<T> results);

    /// <summary>Removes all items.</summary>
    void Clear();
}
