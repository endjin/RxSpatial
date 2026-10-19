namespace MapRunRxSpatial;

/// <summary>What watching a vehicle pair subscribes, and what the map shows for it.</summary>
internal enum PairQuery
{
    /// <summary><c>RxRxDistance</c>: the markers show the distance band (Near vehicle).</summary>
    Distance,

    /// <summary><c>RxRxDistance</c> plus the pair's connection line (Convoy link).</summary>
    DistanceWithConnection,

    /// <summary><c>RxRxIntersect</c> of the two reaches: the markers show whether they overlap.</summary>
    Overlap,

    /// <summary>
    /// <c>RxRxIntersect</c> plus <c>RxRxIntersection</c>: the overlap region (the lens) is also
    /// drawn, labelled with its area.
    /// </summary>
    OverlapWithLens,
}
