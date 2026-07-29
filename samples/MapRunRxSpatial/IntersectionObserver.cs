using Microsoft.SqlServer.Types;

namespace MapRunRxSpatial;

/// <summary>
/// Colours a vehicle by whether its intersection geometry with an area is non-empty. Demo
/// simplification: with several areas, the most recently notified query wins the colour.
/// </summary>
internal sealed class IntersectionObserver(VehicleMarker marker) : VehicleObserverBase<SqlGeography>(marker)
{
    public override void OnNext(SqlGeography intersection)
        => Marker.ApplyColor(intersection.STIsEmpty() ? "blue" : "wheat");
}
