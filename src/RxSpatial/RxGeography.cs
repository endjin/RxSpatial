using System.Data.SqlTypes;
using Microsoft.SqlServer.Types;
using RxSpatial.Lib;
using RxSpatial.Util;

namespace RxSpatial;

/// <summary>
/// A moving object: it observes its own location updates (<see cref="IObserver{T}"/>) and is
/// observable by continuous spatial queries — distance, intersect and intersection — against
/// stationary geographies (<c>Rx*</c>) or other moving objects (<c>RxRx*</c>). Observers are
/// held in a spatial index selected by the <c>preferredDataStruct</c> application setting.
/// </summary>
public class RxGeography : IObserver<SqlGeography>, IObservable<SqlGeography>
{
    private readonly IContainer<IObserver<SqlGeography>> container;
    private readonly GeoList<IObserver<SqlGeography>> observersForLocation = new();
    private readonly Queue<LocationPush> queuedPushs = new();
    private readonly AvgCalc avgLatencyRecorder = new();
    private readonly int bufferLimit;
    private readonly bool isVisualized;

    private SqlGeography? currentLoc;
    private Thread? notifyThread;
    private bool lockTaken;

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
        container = CreateContainer(options);
    }

    /// <summary>The last location pushed to this moving object, or null before the first push.</summary>
    public SqlGeography? loc => currentLoc;

    /// <summary>Average notification latency in milliseconds (performance instrumentation).</summary>
    public double latency => avgLatencyRecorder.Avg;

    public int checkQueuedPushNum() => queuedPushs.Count;

    public virtual void OnNext(SqlGeography g)
    {
        currentLoc = g;
        if (isVisualized)
        {
            // Visualised mode processes every push synchronously on the caller's thread.
            queuedPushs.Enqueue(new LocationPush(g));
            NotifyObservers();
        }
        else
        {
            lock (queuedPushs)
            {
                if (queuedPushs.Count < bufferLimit)
                    queuedPushs.Enqueue(new LocationPush(g));
                else
                    throw new RxSpatialOverLoadException();
            }

            if (!lockTaken)
            {
                notifyThread = new Thread(NotifyObservers);
                notifyThread.Start();
            }
        }
    }

    public virtual void OnCompleted()
    {
    }

    public virtual void OnError(Exception e)
    {
    }

    public IDisposable Subscribe(IObserver<SqlGeography> observer)
    {
        if (!observersForLocation.Contains(observer))
            observersForLocation.Add(observer);

        return new Unsubscriber<SqlGeography>(observersForLocation, observer);
    }

    public IDisposable RxRxIntersect(RxGeography G, IObserver<bool> o)
    {
        IObserver<SqlGeography> observer = new RxRxGeoIntersectObserver(G, o);
        container.subscribeRxRx(observer, G);
        return new Unsubscriber<SqlGeography>(container, observer);
    }

    public IDisposable RxRxIntersection(RxGeography G, IObserver<SqlGeography> o)
    {
        IObserver<SqlGeography> observer = new RxRxGeoIntersectionObserver(G, o);
        container.subscribeRxRx(observer, G);
        return new Unsubscriber<SqlGeography>(container, observer);
    }

    public IDisposable RxRxDistance(RxGeography G, IObserver<SqlDouble> o)
    {
        IObserver<SqlGeography> observer = new RxRxGeoDistObserver(G, o);
        container.subscribeRxRx(observer, G);
        return new Unsubscriber<SqlGeography>(container, observer);
    }

    public IDisposable RxIntersect(SqlGeography G, IObserver<bool> o)
    {
        IObserver<SqlGeography> observer = new RxGeoIntersectObserver(G, o);
        container.subscribeRx(observer, G);
        return new Unsubscriber<SqlGeography>(container, observer);
    }

    public IDisposable RxIntersection(SqlGeography G, IObserver<SqlGeography> o)
    {
        IObserver<SqlGeography> observer = new RxGeoIntersectionObserver(G, o);
        container.subscribeRx(observer, G);
        return new Unsubscriber<SqlGeography>(container, observer);
    }

    public IDisposable RxDistance(SqlGeography G, IObserver<SqlDouble> o)
    {
        IObserver<SqlGeography> observer = new RxGeoDistObserver(G, o);
        container.subscribeRx(observer, G);
        return new Unsubscriber<SqlGeography>(container, observer);
    }

    private static IContainer<IObserver<SqlGeography>> CreateContainer(RxSpatialOptions options)
        => options.PreferredDataStruct switch
        {
            RxGeographyDataStruct.HashMap => new Grid<IObserver<SqlGeography>>(options.Grid),
            RxGeographyDataStruct.RTree => new Tree<IObserver<SqlGeography>>(RxGeographyDataStruct.RTree, options),
            RxGeographyDataStruct.RumTree => new Tree<IObserver<SqlGeography>>(RxGeographyDataStruct.RumTree, options),
            _ => new GeoList<IObserver<SqlGeography>>(),
        };

    private void NotifyObservers()
    {
        if (isVisualized)
        {
            LocationPush push = queuedPushs.Dequeue();
            NotifyLocation(push.Content);
            NotifyObserversInSameZone(push.Content);
        }
        else
        {
            lockTaken = true;
            while (queuedPushs.Count > 0)
            {
                LocationPush push = queuedPushs.Dequeue();
                NotifyObserversInSameZone(push.Content);
                push.FinishProcessing();
                avgLatencyRecorder.recordLatency(push.Latency);
            }

            lockTaken = false;
        }
    }

    private void NotifyLocation(SqlGeography location)
    {
        foreach (IObserver<SqlGeography> observer in observersForLocation)
            observer.OnNext(location);
    }

    private void NotifyObserversInSameZone(SqlGeography location)
    {
        List<IObserver<SqlGeography>>? observers = container.getObjsInSameZone(location);
        if (observers is null)
            return;

        foreach (IObserver<SqlGeography> observer in observers)
            observer.OnNext(location);
    }
}
