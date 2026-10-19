namespace RxSpatial.Experiments;

/// <summary>
/// The four systems compared in the ICDE 2017 paper (§VI). The paper's "SqlSpatial" baseline
/// evaluates every query from scratch on every update; it calls it "List based (SQLSpatial)
/// indexing", which is exactly the List backend.
/// </summary>
internal enum Variant
{
    SqlSpatial,
    Grid,
    RTree,
    RumTree,
}

internal static class VariantExtensions
{
    extension(Variant variant)
    {
        public RxGeographyDataStruct DataStruct => variant switch
        {
            Variant.SqlSpatial => RxGeographyDataStruct.List,
            Variant.Grid => RxGeographyDataStruct.HashMap,
            Variant.RTree => RxGeographyDataStruct.RTree,
            Variant.RumTree => RxGeographyDataStruct.RumTree,
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
        };

        /// <summary>The paper's legend label.</summary>
        public string Label => variant switch
        {
            Variant.SqlSpatial => "SqlSpatial",
            Variant.Grid => "RxSpatial(Grid)",
            Variant.RTree => "RxSpatial(R-Tree)",
            Variant.RumTree => "RxSpatial(RUM-Tree)",
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
        };
    }
}
