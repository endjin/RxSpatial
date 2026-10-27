// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

namespace RxSpatial;

/// <summary>Continuous intersection geometry between two moving objects.</summary>
internal sealed class RxRxIntersectionObserver<TShape, TOps>(IMovingObject<TShape> source, IObserver<TShape> observer)
    : RxRxObserverBase<TShape, TShape>(source, observer)
    where TShape : class
    where TOps : struct, ISpatialOperations<TShape>
{
    // Source.Location is null until the tracked object's first push (preserved behaviour).
    public override void OnNext(TShape location)
        => Observer.OnNext(TOps.Intersection(Source.Location!, location));
}
