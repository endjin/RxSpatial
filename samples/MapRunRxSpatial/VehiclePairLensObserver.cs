using Microsoft.SqlServer.Types;

namespace MapRunRxSpatial;

/// <summary>
/// Overlap tab (<c>RxRxIntersection</c>): redraws the pair's lens, the region where the two
/// reaches overlap; an empty intersection removes it. Both vehicles of the pair update the
/// same shape.
/// </summary>
internal sealed class VehiclePairLensObserver(VehiclePair pair, VehicleMarker marker) : VehicleObserverBase<SqlGeography>(marker)
{
    public override void OnNext(SqlGeography lens) => Marker.UpdateLens(pair, lens);
}
