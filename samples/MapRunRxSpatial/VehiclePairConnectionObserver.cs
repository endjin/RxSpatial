// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using RxSpatial;
using RxSpatial.Geo;

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

        // An RxRx result is only produced once both vehicles have a location, and Convoy link
        // pushes bare points (only the Overlap tab pushes reach discs), so the other end is a
        // GeoPointShape.
        if (other.Location is GeoPointShape otherPoint)
            Marker.UpdateConnectionLine(pair, otherPoint.Position, band);
    }
}
