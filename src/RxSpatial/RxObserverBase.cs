// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

namespace RxSpatial;

/// <summary>
/// Base for continuous-query observers that relate a moving object to a stationary
/// <paramref name="anchor"/> shape and forward each result downstream. Subclasses supply
/// only the query itself.
/// </summary>
internal abstract class RxObserverBase<TShape, TResult>(TShape anchor, IObserver<TResult> observer)
    : IObserver<TShape>
    where TShape : class
{
    protected TShape Anchor { get; } = anchor;

    protected IObserver<TResult> Observer { get; } = observer;

    /// <summary>Re-evaluates the query against the moving object's new location.</summary>
    public abstract void OnNext(TShape location);

    // A continuous query outlives the location stream it listens to: completion and errors
    // of the moving object are not propagated to the query's subscriber (original behaviour).
    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }
}
