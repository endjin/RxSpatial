namespace RxSpatial.Index;

/// <summary>Structure parameters for the R*-tree (Beckmann et al., 1990).</summary>
public record RTreeOptions
{
    /// <summary>Maximum entries per node (M).</summary>
    public int MaxEntries { get; init; } = 16;

    /// <summary>Minimum entries per non-root node (m). The R* optimum is roughly 40% of M.</summary>
    public int MinEntries { get; init; } = 6;

    /// <summary>
    /// Entries re-inserted by R* forced reinsertion on node overflow (roughly 30% of M).
    /// Zero disables forced reinsertion.
    /// </summary>
    public int ReinsertCount { get; init; } = 4;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxEntries, 4);
        ArgumentOutOfRangeException.ThrowIfLessThan(MinEntries, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MinEntries, MaxEntries / 2);
        ArgumentOutOfRangeException.ThrowIfNegative(ReinsertCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ReinsertCount, MaxEntries + 1 - MinEntries);
    }
}
