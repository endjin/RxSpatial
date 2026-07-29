using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>
/// Base for continuous-query observers that relate a moving object to a stationary
/// <paramref name="anchor"/> geography and forward each result downstream.
/// </summary>
internal abstract class RxObserverBase<TResult>(SqlGeography anchor, IObserver<TResult> observer)
{
    protected SqlGeography Anchor { get; } = anchor;

    protected IObserver<TResult> Observer { get; } = observer;
}
