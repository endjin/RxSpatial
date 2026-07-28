namespace RxSpatial;

/// <summary>Spatial index backends selectable via the <c>preferredDataStruct</c> setting.</summary>
internal enum RxGeographyDataStruct
{
    list,
    hashMap,
    rtree,
    rumtree,
}
