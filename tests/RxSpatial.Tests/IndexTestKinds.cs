namespace RxSpatial.Tests;

/// <summary>Which spatial index a shared-contract test row exercises.</summary>
public enum TreeKind
{
    RStar,
    Rum,
}

/// <summary>Index configurations the oracle harness runs a randomized workload against.</summary>
public enum OracleFlavour
{
    /// <summary>Tuned defaults.</summary>
    RStar,

    /// <summary>Forced reinsertion disabled, so overflow always splits.</summary>
    RStarNoReinsert,

    /// <summary>Minimum viable node size, so the tree is deep and splits constantly.</summary>
    RStarTinyNodes,

    /// <summary>Cleaning on every garbage-creating operation.</summary>
    RumEager,

    /// <summary>Cleaning effectively disabled, so every obsolete entry stays in the tree.</summary>
    RumLazy,

    /// <summary>Tiny nodes and frequent cleaning together.</summary>
    RumTinyNodes,
}

/// <summary>The spatial order a structural-churn test drains the tree in.</summary>
public enum DrainOrder
{
    RowMajor,
    Reverse,
    ColumnMajor,
    InsideOut,
}
