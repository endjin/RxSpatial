using RxSpatial;

namespace MapRunRxSpatial;

/// <summary>
/// Like <see cref="VehiclePairDistanceObserver"/>, but also draws the pair's connection line,
/// styled by the distance band. The line is redrawn in every band, so it never goes stale.
/// </summary>
internal sealed class VehiclePairConnectionObserver(VehiclePair pair, RxGeography other, VehicleMarker marker)
    : VehicleObserverBase<double>(marker)
{
    public override void OnNext(double distance)
    {
        DistanceBand band = DistanceBands.ForPair(distance, DemoParameters.ObserveDistance);
        Marker.ApplyColor(DistanceBands.ToColor(band, VehicleColor.Gray));

        // An RxRx result is only produced once both vehicles have a location.
        Marker.UpdateConnectionLine(pair, other.Location!, band);
    }
}
