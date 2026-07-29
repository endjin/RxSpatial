namespace RxSpatial.Index;

/// <summary>Structure parameters for the R*-tree (Beckmann et al., 1990).</summary>
public record RTreeOptions
{
    /// <summary>
    /// Maximum entries per node (M). The default of 9 is tuned for in-memory use — small
    /// nodes keep the O(M²) R* overlap computation cheap on the update path while query
    /// performance stays flat.
    /// </summary>
    public int MaxEntries { get; init; } = 9;

    /// <summary>Minimum entries per non-root node (m). The R* optimum is roughly 40% of M.</summary>
    public int MinEntries { get; init; } = 4;

    /// <summary>
    /// Entries re-inserted by R* forced reinsertion on node overflow (roughly 30% of M).
    /// Zero disables forced reinsertion.
    /// </summary>
    public int ReinsertCount { get; init; } = 3;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxEntries, 4);
        ArgumentOutOfRangeException.ThrowIfLessThan(MinEntries, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MinEntries, MaxEntries / 2);
        ArgumentOutOfRangeException.ThrowIfNegative(ReinsertCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ReinsertCount, MaxEntries + 1 - MinEntries);
    }
}
