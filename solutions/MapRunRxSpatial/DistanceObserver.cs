// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

namespace MapRunRxSpatial;

/// <summary>Colours a vehicle by its distance band to a stationary area.</summary>
internal sealed class DistanceObserver(VehicleMarker marker) : VehicleObserverBase<double>(marker)
{
    public override void OnNext(double distance)
        => Marker.ApplyColor(DistanceBands.ToColor(
            DistanceBands.ForArea(distance, DemoParameters.ObserveDistance), VehicleColor.Blue));
}
