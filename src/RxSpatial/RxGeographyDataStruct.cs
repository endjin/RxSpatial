namespace RxSpatial;

/// <summary>
/// Spatial index backends for holding continuous-query observers, used by both
/// <see cref="RxGeography"/> and <see cref="RxGeometry"/> (the name is historic).
/// </summary>
public enum RxGeographyDataStruct
{
    /// <summary>Plain list: every observer is evaluated on every update.</summary>
    List,

    /// <summary>Fixed lat/long grid (spatial hash) over a configured coverage region.</summary>
    HashMap,

    /// <summary>R*-tree.</summary>
    RTree,

    /// <summary>RUM-tree (R-tree with Update Memo), layered on the R*-tree core — optimised for frequent location updates.</summary>
    RumTree,
}
