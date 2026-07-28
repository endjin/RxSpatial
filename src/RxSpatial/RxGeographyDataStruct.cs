namespace RxSpatial;

/// <summary>Spatial index backends for holding continuous-query observers.</summary>
public enum RxGeographyDataStruct
{
    /// <summary>Plain list: every observer is evaluated on every update.</summary>
    List,

    /// <summary>Fixed lat/long grid (spatial hash) over a configured coverage region.</summary>
    HashMap,

    /// <summary>Classic Guttman R-tree.</summary>
    RTree,

    /// <summary>R-tree with Update Memo — optimised for frequent location updates.</summary>
    RumTree,
}
