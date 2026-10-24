// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using RxSpatial.Backends;

namespace RxSpatial;

/// <summary>Disposable subscription handle that removes an observer from its spatial backend.</summary>
public sealed class Unsubscriber<TShape>(ISpatialBackend<TShape, IObserver<TShape>> observers, IObserver<TShape> observer)
    : IDisposable
    where TShape : class
{
    public void Dispose() => observers.Remove(observer);
}
