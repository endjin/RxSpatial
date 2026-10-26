// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

namespace RxSpatial;

/// <summary>Continuous intersection geometry between a moving object and a stationary shape.</summary>
internal sealed class RxIntersectionObserver<TShape, TOps>(TShape anchor, IObserver<TShape> observer)
    : RxObserverBase<TShape, TShape>(anchor, observer)
    where TShape : class
    where TOps : struct, ISpatialOperations<TShape>
{
    public override void OnNext(TShape location) => Observer.OnNext(TOps.Intersection(Anchor, location));
}
