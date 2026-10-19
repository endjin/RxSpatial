using Microsoft.SqlServer.Types;
using RxSpatial;

namespace MapRunRxSpatial;

/// <summary>
/// One vehicle: an <see cref="RxGeography"/> fed from a recorded trajectory, its map marker,
/// and the keyed continuous-query subscriptions attached to it. Area keys ("area:0") and pair
/// keys ("pair:3-6") are prefixed so the two kinds can never collide.
/// </summary>
internal sealed class MovingObject
{
    private readonly Dictionary<string, IDisposable> subscriptions = [];
    private readonly VehicleMarker marker;
    private readonly MarkerMovementObserver movementObserver;

    public MovingObject(int id, MapLibreMap map, ConnectionLines lines)
    {
        Geography = new RxGeography();
        marker = new VehicleMarker(id, map, lines);
        movementObserver = new MarkerMovementObserver(marker);
        movementObserver.Attach(Geography);
    }

    /// <summary>The moving object other vehicles subscribe against.</summary>
    public RxGeography Geography { get; }

    public void PushLocation(SqlGeography location) => Geography.OnNext(location);

    public void SubscribeIntersect(string name, SqlGeography polygon)
    {
        string key = AreaKey(name);
        if (!subscriptions.ContainsKey(key))
            subscriptions.Add(key, Geography.RxIntersect(polygon, new IntersectObserver(marker)));
    }

    public void SubscribeDistance(string name, SqlGeography polygon)
    {
        string key = AreaKey(name);
        if (!subscriptions.ContainsKey(key))
            subscriptions.Add(key, Geography.RxDistance(polygon, new DistanceObserver(marker)));
    }

    /// <summary>
    /// Watches the distance from this vehicle to <paramref name="other"/>, optionally drawing
    /// the pair's connection line. Returns false if the pair is already watched.
    /// </summary>
    public bool WatchPair(VehiclePair pair, RxGeography other, bool drawConnection)
    {
        if (subscriptions.ContainsKey(pair.SubscriptionKey))
            return false;

        IObserver<double> observer = drawConnection
            ? new VehiclePairConnectionObserver(pair, other, marker)
            : new VehiclePairDistanceObserver(marker);
        subscriptions.Add(pair.SubscriptionKey, Geography.RxRxDistance(other, observer));
        return true;
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

    private static string AreaKey(string name) => "area:" + name;
}
