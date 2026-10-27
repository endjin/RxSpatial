// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

namespace MapRunRxSpatial;

/// <summary>
/// Colours a vehicle by its distance band to another moving vehicle: Gray while watched but
/// out of range, Red below the minimum safe distance.
/// </summary>
internal sealed class VehiclePairDistanceObserver(VehicleMarker marker) : VehicleObserverBase<double>(marker)
{
    public override void OnNext(double distance)
        => Marker.ApplyColor(DistanceBands.ToColor(
            DistanceBands.ForPair(distance, DemoParameters.ObserveDistance), VehicleColor.Gray));
}
