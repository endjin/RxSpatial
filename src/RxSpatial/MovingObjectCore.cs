using RxSpatial.Backends;

namespace RxSpatial;

/// <summary>
/// The moving-object engine behind the public facades: the push queue and its drainer,
/// latency instrumentation, the location observers and the spatial backend of continuous-query
/// observers. Shape-specific work comes from <typeparamref name="TOps"/>, a stateless struct
/// whose static members the JIT calls directly.
/// </summary>
/// <typeparam name="TShape">The shape type of a location.</typeparam>
/// <typeparam name="TOps">The shape's spatial operations.</typeparam>
internal sealed class MovingObjectCore<TShape, TOps> : IMovingObject<TShape>
    where TShape : class
    where TOps : struct, ISpatialOperations<TShape>
{
    private readonly ISpatialBackend<TShape, IObserver<TShape>> backend;
    private readonly ListBackend<TShape, IObserver<TShape>> locationObservers = [];
    private readonly Queue<LocationPush<TShape>> pushQueue = new();
    private readonly RunningAverage averageLatency = new();
    private readonly int bufferLimit;
    private readonly bool isVisualized;

    private TShape? currentLocation;
    private bool drainerRunning;

    public MovingObjectCore(RxSpatialOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        bufferLimit = options.QueueLimit;
        isVisualized = options.IsVisualized;
        backend = CreateBackend(options);
    }

    public TShape? Location => currentLocation;

    public double AverageLatency => averageLatency.Average;

    public int QueuedPushCount => pushQueue.Count;

    public void Push(TShape location)
    {
        currentLocation = location;
        if (isVisualized)
        {
            // Visualised mode processes every push synchronously on the caller's thread.
            pushQueue.Enqueue(new LocationPush<TShape>(location));
            ProcessQueuedPush();
        }
        else
        {
            bool startDrainer = false;
            lock (pushQueue)
            {
                if (pushQueue.Count < bufferLimit)
                    pushQueue.Enqueue(new LocationPush<TShape>(location));
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

            // One drainer at a time, dispatched to the thread pool. The original started a
            // fresh OS thread per burst, which costs far more than the work it was created
            // for: with the queue usually empty (the default QueueLimit is 1) a moving object
            // paid a thread construction on almost every push. QueueUserWorkItem (not the
            // Unsafe overload) keeps the ExecutionContext flow that Thread.Start gave the
            // notification callbacks.
            if (startDrainer)
                ThreadPool.QueueUserWorkItem(static self => self.DrainQueue(), this, preferLocal: false);
        }
    }

    public IDisposable Subscribe(IObserver<TShape> observer)
    {
        if (!locationObservers.Contains(observer))
            locationObservers.Add(observer);

        return new Unsubscriber<TShape>(locationObservers, observer);
    }

    /// <summary>
    /// Registers a query observer that follows <paramref name="other"/>. Pass the public
    /// facade, so the backend's location tracker subscribes through it.
    /// </summary>
    public IDisposable SubscribeToMoving(IObserver<TShape> observer, IMovingObject<TShape> other)
    {
        backend.Subscribe(observer, other);
        return new Unsubscriber<TShape>(backend, observer);
    }

    /// <summary>Registers a query observer anchored at the stationary <paramref name="anchor"/>.</summary>
    public IDisposable SubscribeToStationary(IObserver<TShape> observer, TShape anchor)
    {
        backend.Subscribe(observer, anchor);
        return new Unsubscriber<TShape>(backend, observer);
    }

    private static ISpatialBackend<TShape, IObserver<TShape>> CreateBackend(RxSpatialOptions options)
        => options.PreferredDataStruct switch
        {
            RxGeographyDataStruct.HashMap
                => new GridBackend<TShape, TOps, IObserver<TShape>>(TOps.GridLayoutOf(options)),
            RxGeographyDataStruct.RTree
                => new TreeBackend<TShape, TOps, IObserver<TShape>>(RxGeographyDataStruct.RTree, options),
            RxGeographyDataStruct.RumTree
                => new TreeBackend<TShape, TOps, IObserver<TShape>>(RxGeographyDataStruct.RumTree, options),
            _ => new ListBackend<TShape, IObserver<TShape>>(),
        };

    private void ProcessQueuedPush()
    {
        LocationPush<TShape> push = pushQueue.Dequeue();
        NotifyLocationObservers(push.Content);
        NotifyZoneObservers(push.Content);
    }

    private void DrainQueue()
    {
        while (true)
        {
            LocationPush<TShape> push;
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

    private void NotifyLocationObservers(TShape location)
    {
        foreach (IObserver<TShape> observer in locationObservers)
            observer.OnNext(location);
    }

    private void NotifyZoneObservers(TShape location)
    {
        foreach (IObserver<TShape> observer in backend.GetObserversInZone(location))
            observer.OnNext(location);
    }
}
