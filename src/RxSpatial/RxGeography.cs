using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;
using RxSpatial.Backends;

namespace RxSpatial;

/// <summary>
/// A moving object: it observes its own location updates (<see cref="IObserver{T}"/>) and is
/// observable by continuous spatial queries — distance, intersect and intersection — against
/// stationary geographies (<c>Rx*</c>) or other moving objects (<c>RxRx*</c>). Observers are
/// held in the spatial backend selected by <see cref="RxSpatialOptions.PreferredDataStruct"/>.
/// </summary>
public class RxGeography : IObserver<SqlGeography>, IObservable<SqlGeography>
{
    private readonly ISpatialBackend<IObserver<SqlGeography>> backend;
    private readonly ListBackend<IObserver<SqlGeography>> locationObservers = [];
    private readonly Queue<LocationPush> pushQueue = new();
    private readonly RunningAverage averageLatency = new();
    private readonly int bufferLimit;
    private readonly bool isVisualized;

    private SqlGeography? currentLocation;
    private bool drainerRunning;

    /// <summary>Creates a moving object configured by <see cref="RxSpatialOptions.Default"/>.</summary>
    public RxGeography()
        : this(RxSpatialOptions.Default)
    {
    }

    /// <summary>Creates a moving object with explicit settings.</summary>
    public RxGeography(RxSpatialOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        bufferLimit = options.QueueLimit;
        isVisualized = options.IsVisualized;
        backend = CreateBackend(options);
    }

    /// <summary>The last location pushed to this moving object, or null before the first push.</summary>
    public SqlGeography? Location => currentLocation;

    /// <summary>Average notification latency in milliseconds (performance instrumentation).</summary>
    public double AverageLatency => averageLatency.Average;

    /// <summary>Location pushes buffered but not yet processed (always 0 in visualised mode).</summary>
    public int QueuedPushCount => pushQueue.Count;

    public virtual void OnNext(SqlGeography location)
    {
        currentLocation = location;
        if (isVisualized)
        {
            // Visualised mode processes every push synchronously on the caller's thread.
            pushQueue.Enqueue(new LocationPush(location));
            ProcessQueuedPush();
        }
        else
        {
            bool startDrainer = false;
            lock (pushQueue)
            {
                if (pushQueue.Count < bufferLimit)
                    pushQueue.Enqueue(new LocationPush(location));
                else
                    throw new RxSpatialOverloadException();

                // Decide under the lock whether a drainer thread is needed; the old
                // unsynchronised flag check could start two drainers racing over the queue.
                if (!drainerRunning)
                {
                    drainerRunning = true;
                    startDrainer = true;
                }
            }

            if (startDrainer)
                new Thread(DrainQueue).Start();
        }
    }

    public virtual void OnCompleted()
    {
    }

    public virtual void OnError(Exception error)
    {
    }

    public IDisposable Subscribe(IObserver<SqlGeography> observer)
    {
        if (!locationObservers.Contains(observer))
            locationObservers.Add(observer);

        return new Unsubscriber<SqlGeography>(locationObservers, observer);
    }

    public IDisposable RxRxIntersect(RxGeography other, IObserver<bool> observer)
        => SubscribeToMoving(new RxRxIntersectObserver(other, observer), other);

    public IDisposable RxRxIntersection(RxGeography other, IObserver<SqlGeography> observer)
        => SubscribeToMoving(new RxRxIntersectionObserver(other, observer), other);

    public IDisposable RxRxDistance(RxGeography other, IObserver<SqlDouble> observer)
        => SubscribeToMoving(new RxRxDistanceObserver(other, observer), other);

    public IDisposable RxIntersect(SqlGeography geography, IObserver<bool> observer)
        => SubscribeToStationary(new RxIntersectObserver(geography, observer), geography);

    public IDisposable RxIntersection(SqlGeography geography, IObserver<SqlGeography> observer)
        => SubscribeToStationary(new RxIntersectionObserver(geography, observer), geography);

    public IDisposable RxDistance(SqlGeography geography, IObserver<SqlDouble> observer)
        => SubscribeToStationary(new RxDistanceObserver(geography, observer), geography);

    private Unsubscriber<SqlGeography> SubscribeToMoving(IObserver<SqlGeography> observer, RxGeography other)
    {
        backend.Subscribe(observer, other);
        return new Unsubscriber<SqlGeography>(backend, observer);
    }

    private Unsubscriber<SqlGeography> SubscribeToStationary(
        IObserver<SqlGeography> observer, SqlGeography geography)
    {
        backend.Subscribe(observer, geography);
        return new Unsubscriber<SqlGeography>(backend, observer);
    }

    private static ISpatialBackend<IObserver<SqlGeography>> CreateBackend(RxSpatialOptions options)
        => options.PreferredDataStruct switch
        {
            RxGeographyDataStruct.HashMap => new GridBackend<IObserver<SqlGeography>>(options.Grid),
            RxGeographyDataStruct.RTree => new TreeBackend<IObserver<SqlGeography>>(RxGeographyDataStruct.RTree, options),
            RxGeographyDataStruct.RumTree => new TreeBackend<IObserver<SqlGeography>>(RxGeographyDataStruct.RumTree, options),
            _ => new ListBackend<IObserver<SqlGeography>>(),
        };

    private void ProcessQueuedPush()
    {
        LocationPush push = pushQueue.Dequeue();
        NotifyLocationObservers(push.Content);
        NotifyZoneObservers(push.Content);
    }

    private void DrainQueue()
    {
        while (true)
        {
            LocationPush push;
            lock (pushQueue)
            {
                if (pushQueue.Count == 0)
                {
                    drainerRunning = false;
                    return;
                }

                push = pushQueue.Dequeue();
            }

            NotifyZoneObservers(push.Content);
            push.FinishProcessing();
            averageLatency.Record(push.Latency);
        }
    }

    private void NotifyLocationObservers(SqlGeography location)
    {
        foreach (IObserver<SqlGeography> observer in locationObservers)
            observer.OnNext(location);
    }

    private void NotifyZoneObservers(SqlGeography location)
    {
        foreach (IObserver<SqlGeography> observer in backend.GetObserversInZone(location))
            observer.OnNext(location);
    }
}
