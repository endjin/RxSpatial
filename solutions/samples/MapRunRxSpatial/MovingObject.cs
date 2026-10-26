// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using System.Reactive.Disposables;
using RxSpatial;
using RxSpatial.Geo;

namespace MapRunRxSpatial;

/// <summary>
/// One vehicle: an <see cref="RxGeography"/> fed from a recorded trajectory, its map marker,
/// and the keyed continuous-query subscriptions attached to it. Area keys ("area:0") and pair
/// keys ("pair:3-6") are prefixed so the two kinds can never collide.
/// </summary>
internal sealed class MovingObject
{
    private const string AreaKeyPrefix = "area:";

    private readonly Dictionary<string, IDisposable> subscriptions = [];
    private readonly int id;
    private readonly VehicleMarker marker;
    private readonly MarkerMovementObserver movementObserver;
    private readonly HashSet<string> confinementKeys = [];
    private bool insideConfinementThisPush;
    private VehicleFix fix;

    public MovingObject(int id, MapLibreMap map, ConnectionLines lines, OverlapShapes overlaps)
    {
        this.id = id;
        Geography = new RxGeography();
        marker = new VehicleMarker(id, map, lines, overlaps);
        movementObserver = new MarkerMovementObserver(marker, () => fix);
        movementObserver.Attach(Geography);
    }

    /// <summary>The moving object other vehicles subscribe against.</summary>
    public RxGeography Geography { get; }

    /// <summary>
    /// Pushes a position and, once every observer has seen it, raises a confinement breach if
    /// the vehicle has confinement areas and is inside none of them. On the Overlap tab the
    /// pushed location is the vehicle's reach, the exact <see cref="GeoCircle"/> of
    /// <see cref="DemoParameters.Reach"/> metres around <paramref name="position"/>, so the
    /// intersect and intersection queries compare regions; the marker stays at
    /// <paramref name="position"/> itself.
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
    public void PushLocation(GeoPointShape position)
    {
        GeoCircle? reach = DemoParameters.ActiveScenario == Scenario.RxRxIntersection
            ? new GeoCircle(position.Position, DemoParameters.Reach)
            : null;
        fix = new VehicleFix(position.Position, reach);
        DemoMetrics.Push();
        insideConfinementThisPush = false;
        Geography.OnNext(reach ?? (GeoShape)position);
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

    public void SubscribeDistance(string name, GeoShape polygon)
    {
        string key = AreaKey(name);
        if (!subscriptions.ContainsKey(key))
            subscriptions.Add(key, Geography.RxDistance(polygon, new DistanceObserver(marker)));
    }

    /// <summary>Overlap tab: shades the part of this vehicle's reach inside the zone.</summary>
    public void SubscribeZoneOverlap(DemoPolygon zone)
    {
        string key = AreaKey(zone.Name);
        if (subscriptions.ContainsKey(key))
            return;

        string shapeId = FormattableString.Invariant($"{zone.ShapeId}-v{id}");
        subscriptions.Add(key, Geography.RxIntersection(zone.Geography, new ZoneOverlapObserver(shapeId, marker)));
    }

    /// <summary>
    /// Watches this vehicle's relation to <paramref name="other"/> with the given query.
    /// Returns false if the pair is already watched.
    /// </summary>
    public bool WatchPair(VehiclePair pair, RxGeography other, PairQuery query)
    {
        if (subscriptions.ContainsKey(pair.SubscriptionKey))
            return false;

        IDisposable subscription = query switch
        {
            PairQuery.Distance => Geography.RxRxDistance(other, new VehiclePairDistanceObserver(marker)),
            PairQuery.DistanceWithConnection => Geography.RxRxDistance(other, new VehiclePairConnectionObserver(pair, other, marker)),
            PairQuery.Overlap => Geography.RxRxIntersect(other, new VehiclePairOverlapObserver(marker)),
            PairQuery.OverlapWithLens => new CompositeDisposable(
                Geography.RxRxIntersect(other, new VehiclePairOverlapObserver(marker)),
                Geography.RxRxIntersection(other, new VehiclePairLensObserver(pair, marker))),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query, "Unknown pair query."),
        };
        subscriptions.Add(pair.SubscriptionKey, subscription);
        return true;
    }

    public void UnsubscribeAll()
    {
        foreach (IDisposable subscription in subscriptions.Values)
            subscription.Dispose();

        subscriptions.Clear();
        confinementKeys.Clear();
    }

    /// <summary>Stops watching every pair; the area subscriptions stay.</summary>
    public void UnsubscribePairs() => Unsubscribe(key => !key.StartsWith(AreaKeyPrefix, StringComparison.Ordinal));

    /// <summary>Stops watching every area; the pair subscriptions stay.</summary>
    public void UnsubscribeAreas()
    {
        Unsubscribe(key => key.StartsWith(AreaKeyPrefix, StringComparison.Ordinal));
        confinementKeys.Clear();
    }

    public void Remove()
    {
        UnsubscribeAll();
        movementObserver.OnCompleted();
    }

    private void Unsubscribe(Func<string, bool> selected)
    {
        foreach (string key in subscriptions.Keys.Where(selected).ToList())
        {
            subscriptions[key].Dispose();
            subscriptions.Remove(key);
        }
    }

    private void ReportInsideConfinement() => insideConfinementThisPush = true;

    private static string AreaKey(string name) => AreaKeyPrefix + name;
}
