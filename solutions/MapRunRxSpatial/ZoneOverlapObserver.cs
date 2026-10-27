// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using RxSpatial.Geo;

namespace MapRunRxSpatial;

/// <summary>
/// Overlap tab (<c>RxIntersection</c>): shades the part of the vehicle's reach inside a zone;
/// an empty intersection removes the shading.
/// </summary>
internal sealed class ZoneOverlapObserver(string shapeId, VehicleMarker marker) : VehicleObserverBase<GeoShape>(marker)
{
    public override void OnNext(GeoShape overlap) => Marker.UpdateZoneOverlap(shapeId, overlap);
}
