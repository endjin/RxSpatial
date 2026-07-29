using Microsoft.SqlServer.Types;
using RxSpatial;

namespace MapRunRxSpatial;

/// <summary>
/// One vehicle: an <see cref="RxGeography"/> fed from a recorded trajectory, its map marker,
/// and the named continuous-query subscriptions attached to it.
/// </summary>
internal sealed class MovingObject
{
    private readonly Dictionary<string, IDisposable> subscriptions = [];
    private readonly VehicleMarker marker;
    private readonly MarkerMovementObserver movementObserver;

    public MovingObject(int id, MapLibreMap map, PolygonManager polygons)
    {
        Geography = new RxGeography();
        marker = new VehicleMarker(id, map, polygons);
        movementObserver = new MarkerMovementObserver(marker);
        movementObserver.Attach(Geography);
    }

    /// <summary>The moving object other vehicles subscribe against.</summary>
    public RxGeography Geography { get; }

    public void PushLocation(SqlGeography location) => Geography.OnNext(location);

    public void SubscribeIntersect(string name, SqlGeography polygon)
    {
        if (!subscriptions.ContainsKey(name))
            subscriptions.Add(name, Geography.RxIntersect(polygon, new IntersectObserver(marker)));
    }

    public void SubscribeIntersection(string name, SqlGeography polygon)
    {
        if (!subscriptions.ContainsKey(name))
            subscriptions.Add(name, Geography.RxIntersection(polygon, new IntersectionObserver(marker)));
    }

    public void SubscribeDistance(string name, SqlGeography polygon)
    {
        if (!subscriptions.ContainsKey(name))
            subscriptions.Add(name, Geography.RxDistance(polygon, new DistanceObserver(marker)));
    }

    public bool SubscribeRxRxDistance(string name, RxGeography other)
    {
        if (subscriptions.ContainsKey(name))
            return false;

        subscriptions.Add(name, Geography.RxRxDistance(other, new RxRxDistanceObserver(marker)));
        return true;
    }

    public bool SubscribeRxRxDistanceConnection(string name, RxGeography other)
    {
        if (subscriptions.ContainsKey(name))
            return false;

        subscriptions.Add(name, Geography.RxRxDistance(other, new RxRxDistanceConnectionObserver(other, marker)));
        return true;
    }

    public void Unsubscribe(string name)
    {
        // Remove the disposed subscription so the name can be subscribed again later.
        if (subscriptions.Remove(name, out IDisposable? subscription))
            subscription.Dispose();
    }

    public void UnsubscribeAll()
    {
        foreach (IDisposable subscription in subscriptions.Values)
            subscription.Dispose();

        subscriptions.Clear();
    }

    public void Remove()
    {
        UnsubscribeAll();
        movementObserver.OnCompleted();
    }
}
