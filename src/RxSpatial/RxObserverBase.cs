using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>
/// Base for continuous-query observers that relate a moving object to a stationary
/// <paramref name="anchor"/> geography and forward each result downstream. Subclasses
/// supply only the query itself.
/// </summary>
internal abstract class RxObserverBase<TResult>(SqlGeography anchor, IObserver<TResult> observer)
    : IObserver<SqlGeography>
{
    protected SqlGeography Anchor { get; } = anchor;

    protected IObserver<TResult> Observer { get; } = observer;

    /// <summary>Re-evaluates the query against the moving object's new location.</summary>
    public abstract void OnNext(SqlGeography location);

    // A continuous query outlives the location stream it listens to: completion and errors
    // of the moving object are not propagated to the query's subscriber (original behaviour).
    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }
}
