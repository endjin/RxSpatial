namespace RxSpatial.Index;

/// <summary>Structure parameters for the RUM-tree (R-tree with Update Memo).</summary>
public sealed record RumTreeOptions : RTreeOptions
{
    public RumTreeOptions()
    {
        // The RUM-tree carries obsolete entries between cleanings, so it prefers larger
        // leaves than the plain R*-tree: each cleaning-token visit reclaims more garbage.
        MaxEntries = 16;
        MinEntries = 6;
        ReinsertCount = 4;
    }

    /// <summary>
    /// Number of garbage-creating operations (updates of a live item, removals) between
    /// advances of the cleaning token that purges obsolete entries from one leaf.
    /// Steady-state pending garbage settles near CleaningInterval × leaf count, so keep
    /// the interval well below the average leaf fill or garbage outruns the cleaner.
    /// </summary>
    public int CleaningInterval { get; init; } = 5;

    internal override void Validate()
    {
        base.Validate();
        ArgumentOutOfRangeException.ThrowIfLessThan(CleaningInterval, 1);
    }
}
