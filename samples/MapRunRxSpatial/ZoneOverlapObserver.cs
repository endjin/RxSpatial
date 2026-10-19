using Microsoft.SqlServer.Types;

namespace MapRunRxSpatial;

/// <summary>
/// Overlap tab (<c>RxIntersection</c>): shades the part of the vehicle's reach inside a zone;
/// an empty intersection removes the shading.
/// </summary>
internal sealed class ZoneOverlapObserver(string shapeId, VehicleMarker marker) : VehicleObserverBase<SqlGeography>(marker)
{
    public override void OnNext(SqlGeography overlap) => Marker.UpdateZoneOverlap(shapeId, overlap);
}
