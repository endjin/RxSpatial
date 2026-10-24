// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

namespace MapRunRxSpatial;

/// <summary>
/// Reports that a vehicle is inside one of its confinement areas during the current push. It
/// never colours the marker itself: the breach decision (outside every confinement area) is
/// taken by <see cref="MovingObject.PushLocation"/> once the push has notified every observer.
/// </summary>
internal sealed class ConfinementObserver(VehicleMarker marker, Action reportInside) : VehicleObserverBase<bool>(marker)
{
    public override void OnNext(bool inside)
    {
        if (inside)
            reportInside();
    }
}
