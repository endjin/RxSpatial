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
    private readonly HashSet<string> confinementKeys = [];
    private bool insideConfinementThisPush;

    public MovingObject(int id, MapLibreMap map, ConnectionLines lines)
    {
        Geography = new RxGeography();
        marker = new VehicleMarker(id, map, lines);
        movementObserver = new MarkerMovementObserver(marker);
        movementObserver.Attach(Geography);
    }

    /// <summary>The moving object other vehicles subscribe against.</summary>
    public RxGeography Geography { get; }

    /// <summary>
    /// Pushes a location and, once every observer has seen it, raises a confinement breach if
    /// the vehicle has confinement areas and is inside none of them.
    /// </summary>
    /// <remarks>
    /// The breach cannot be an inverted intersect observer ("not intersecting, so red"): tree
    /// and grid backends only notify an observer whose zone bounds intersect the pushed
    /// location, so a vehicle far outside a confinement area never hears from that area's
    /// observer at all. Instead each push is bracketed: the flag is cleared, the
    /// <see cref="ConfinementObserver"/>s set it if any area contains the vehicle, and the
    /// decision is taken after <see cref="RxGeography.OnNext"/> returns. That relies on the
    /// demo's visualised mode (forced in <see cref="App"/>), which notifies location and zone
    /// observers synchronously inside OnNext, so the marker's reset to Blue has happened by then.
    /// </remarks>
    public void PushLocation(SqlGeography location)
    {
        insideConfinementThisPush = false;
        Geography.OnNext(location);
        if (confinementKeys.Count > 0 && !insideConfinementThisPush)
            marker.ApplyColor(VehicleColor.Red);
    }

    /// <summary>
    /// Watches an area with the observer its kind calls for: a restricted area alerts while
    /// the vehicle is inside it, a confinement area contributes to the end-of-push breach check.
    /// </summary>
    public void SubscribeIntersect(DemoPolygon area)
    {
        string key = AreaKey(area.Name);
        if (subscriptions.ContainsKey(key))
            return;

        if (area.Kind == AreaKind.Confinement)
        {
            subscriptions.Add(key, Geography.RxIntersect(area.Geography, new ConfinementObserver(marker, ReportInsideConfinement)));
            confinementKeys.Add(key);
        }
        else
        {
            subscriptions.Add(key, Geography.RxIntersect(area.Geography, new IntersectObserver(marker)));
        }
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
        confinementKeys.Clear();
    }

    public void Remove()
    {
        UnsubscribeAll();
        movementObserver.OnCompleted();
    }

    private void ReportInsideConfinement() => insideConfinementThisPush = true;

    private static string AreaKey(string name) => "area:" + name;
}
