// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

namespace RxSpatial;

/// <summary>Continuous intersection test between a moving object and a stationary shape.</summary>
internal sealed class RxIntersectObserver<TShape, TOps>(TShape anchor, IObserver<bool> observer)
    : RxObserverBase<TShape, bool>(anchor, observer)
    where TShape : class
    where TOps : struct, ISpatialOperations<TShape>
{
    public override void OnNext(TShape location) => Observer.OnNext(TOps.Intersects(Anchor, location));
}
