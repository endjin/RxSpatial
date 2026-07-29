using Microsoft.SqlServer.Types;

namespace RxSpatial;

/// <summary>
/// Base for continuous-query observers that relate two moving objects: the tracked
/// <paramref name="source"/> and the object whose pushes drive <see cref="OnNext"/>.
/// Subclasses supply only the query itself.
/// </summary>
internal abstract class RxRxObserverBase<TResult>(RxGeography source, IObserver<TResult> observer)
    : IObserver<SqlGeography>
{
    protected RxGeography Source { get; } = source;

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
