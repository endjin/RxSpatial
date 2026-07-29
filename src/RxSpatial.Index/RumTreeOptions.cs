namespace RxSpatial.Index;

/// <summary>Structure parameters for the RUM-tree (R-tree with Update Memo).</summary>
public sealed record RumTreeOptions : RTreeOptions
{
    /// <summary>
    /// Number of garbage-creating operations (updates of a live item, removals) between
    /// advances of the cleaning token that purges obsolete entries from one leaf.
    /// </summary>
    public int CleaningInterval { get; init; } = 10;

    internal new void Validate()
    {
        base.Validate();
        ArgumentOutOfRangeException.ThrowIfLessThan(CleaningInterval, 1);
    }
}
