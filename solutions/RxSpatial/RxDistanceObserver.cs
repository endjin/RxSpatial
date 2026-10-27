// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

namespace RxSpatial;

/// <summary>Continuous distance query between a moving object and a stationary shape.</summary>
internal sealed class RxDistanceObserver<TShape, TOps>(TShape anchor, IObserver<double> observer)
    : RxObserverBase<TShape, double>(anchor, observer)
    where TShape : class
    where TOps : struct, ISpatialOperations<TShape>
{
    public override void OnNext(TShape location) => Observer.OnNext(TOps.Distance(Anchor, location));
}
