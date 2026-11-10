// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using RxSpatial.Geo;

namespace MapRunRxSpatial;

/// <summary>The outcome of <see cref="MovingObjectManager.WatchPair"/>.</summary>
internal enum PairSubscriptionResult
{
    Added,
    SameVehicle,
    NotOnMap,
    AlreadyWatched,
}

/// <summary>
/// Replays the recorded vehicle trajectories on a timer and keeps the fleet subscribed to
/// the active scenario's continuous queries.
/// </summary>
internal sealed class MovingObjectManager
{
    private const int TrajectoryCount = 20;

    /// <summary>Catch-up limit: a firing runs at most this many due ticks (see <see cref="OnTimer"/>).</summary>
    private const int MaxTicksPerFiring = 3;

    private readonly Dictionary<int, MovingObject> vehicles = [];
    private readonly TrajectoryCursor[] trajectories = new TrajectoryCursor[TrajectoryCount];
    private readonly MapLibreMap map;
    private readonly PolygonManager polygons;
    private readonly ConnectionLines lines;
    private readonly OverlapShapes overlaps;
    private readonly List<VehiclePair> pendingPairs = [];
    private readonly Stopwatch clock = new();
    private PairQuery pendingPairsQuery;
    private DispatcherTimer? timer;
    private TimeSpan tickPeriod;
    private TimeSpan nextTickDue;
    private int previousVehicleCount = -1;

    public MovingObjectManager(MapLibreMap map, PolygonManager polygons, ConnectionLines lines, OverlapShapes overlaps)
    {
        this.map = map;
        this.polygons = polygons;
        this.lines = lines;
        this.overlaps = overlaps;
        LoadTrajectories();
    }

    public bool TrafficStarted { get; private set; }

    /// <summary>The south-west corner of the rectangle every recorded trajectory stays inside.</summary>
    public GeoPoint TrajectorySouthWest { get; private set; }

    /// <summary>The north-east corner of the rectangle every recorded trajectory stays inside.</summary>
    public GeoPoint TrajectoryNorthEast { get; private set; }

    /// <summary>
    /// Raised (on the UI thread) when the last pair waiting for its vehicles is watched, so a
    /// status line that promised "watched from Start" can say the pairs are now watched.
    /// </summary>
    public event Action? PendingPairsWatched;

    public void StartTraffic()
    {
        // Stop any running timer first: repeated Start clicks must not stack timers
        // (each stacked timer would multiply the traffic speed).
        StopTraffic();
        tickPeriod = TimeSpan.FromMilliseconds(DemoParameters.MovementIntervalMilliseconds);
        nextTickDue = tickPeriod;
        clock.Restart();
        timer = new DispatcherTimer { Interval = tickPeriod };
        timer.Tick += OnTimer;
        timer.Start();
        TrafficStarted = true;
    }

    public void StopTraffic() => timer?.Stop();

    public void RestartTraffic()
    {
        if (TrafficStarted)
            StartTraffic();
    }

    /// <summary>Subscribes every vehicle to an area newly drawn on the Geofence tab, restricted or confinement.</summary>
    public void SubscribeIntersectToNewPolygon(DemoPolygon area)
    {
        foreach (MovingObject vehicle in vehicles.Values)
            vehicle.SubscribeIntersect(area);
    }

    public void SubscribeDistanceToNewPolygon(string name, GeoShape polygon)
    {
        foreach (MovingObject vehicle in vehicles.Values)
            vehicle.SubscribeDistance(name, polygon);
    }

    /// <summary>Overlap tab: subscribes every vehicle to a newly drawn zone.</summary>
    public void SubscribeZoneOverlapToNewPolygon(DemoPolygon zone)
    {
        foreach (MovingObject vehicle in vehicles.Values)
            vehicle.SubscribeZoneOverlap(zone);
    }

    /// <summary>
    /// Watches two vehicles with the given query in both directions (an RxRx observer fires
    /// only when its own vehicle moves); a connection line or lens is shared by the pair.
    /// </summary>
    public PairSubscriptionResult WatchPair(int id1, int id2, PairQuery query)
    {
        if (id1 == id2)
            return PairSubscriptionResult.SameVehicle;

        // Membership checks rather than count-based bounds: a vehicle is missing only before
        // Start, since every trajectory begins with a "newpoint" action.
        if (!vehicles.TryGetValue(id1, out MovingObject? first)
            || !vehicles.TryGetValue(id2, out MovingObject? second))
            return PairSubscriptionResult.NotOnMap;

        VehiclePair pair = VehiclePair.Of(id1, id2);
        if (!first.WatchPair(pair, second.Geography, query))
            return PairSubscriptionResult.AlreadyWatched;

        second.WatchPair(pair, first.Geography, query);
        return PairSubscriptionResult.Added;
    }

    /// <summary>
    /// Watches the given pairs as soon as both of their vehicles are on the map: vehicles
    /// appear only on the first traffic tick after Start or a reset. Replaces any pairs still
    /// pending.
    /// </summary>
    public void WatchPairsWhenOnMap(IEnumerable<VehiclePair> pairs, PairQuery query)
    {
        pendingPairs.Clear();
        pendingPairs.AddRange(pairs);
        pendingPairsQuery = query;
        WatchPendingPairs();
    }

    /// <summary>The number of distinct pairs among the replayed vehicles: 190 for 20 vehicles.</summary>
    public static int AllPairsCount => TrajectoryCount * (TrajectoryCount - 1) / 2;

    /// <summary>
    /// The self join of ICDE 2017 section V.B: watches every pair of vehicles with
    /// <paramref name="query"/> (the distance on Near vehicle, the overlap on Overlap; never a
    /// line or lens per pair). Each pair is watched in both directions, so 20 vehicles need 380
    /// observers. Already-watched pairs are skipped, and a pair whose vehicles are not on the
    /// map yet is watched as soon as they appear, like the pre-set pairs.
    /// </summary>
    /// <returns>The number of pairs still waiting for their vehicles; 0 when all are watched.</returns>
    public int WatchAllPairs(PairQuery query)
    {
        List<VehiclePair> pairs = new(AllPairsCount);
        for (int first = 0; first < TrajectoryCount; first++)
        {
            for (int second = first + 1; second < TrajectoryCount; second++)
                pairs.Add(VehiclePair.Of(first, second));
        }

        // Replaces the pending pre-set pairs too, but they are a subset of these.
        WatchPairsWhenOnMap(pairs, query);
        return pendingPairs.Count;
    }

    /// <summary>Stops watching every pair and area, including pairs not yet on the map.</summary>
    public void UnsubscribeAll()
    {
        pendingPairs.Clear();
        foreach (MovingObject vehicle in vehicles.Values)
            vehicle.UnsubscribeAll();
    }

    /// <summary>
    /// Stops watching every pair, including pairs not yet on the map, and removes the pairs'
    /// connection lines and lenses; the areas stay.
    /// </summary>
    public void UnwatchAllPairs()
    {
        pendingPairs.Clear();
        foreach (MovingObject vehicle in vehicles.Values)
            vehicle.UnsubscribePairs();

        lines.Clear();
        overlaps.ClearLenses();
    }

    /// <summary>Stops watching every area and removes the zone overlaps; the pairs stay.</summary>
    public void UnsubscribeAllAreas()
    {
        foreach (MovingObject vehicle in vehicles.Values)
            vehicle.UnsubscribeAreas();

        overlaps.ClearZoneOverlaps();
    }

    /// <summary>
    /// Removes every vehicle so the next traffic tick recreates them - used when the
    /// active tab changes the spatial backend, which is baked into each RxGeography
    /// at construction.
    /// </summary>
    public void ClearAllVehicles()
    {
        foreach (MovingObject vehicle in vehicles.Values)
            vehicle.Remove();

        vehicles.Clear();
        previousVehicleCount = -1;

        // Rewind every trajectory to its "newpoint" action so the next tick
        // recreates the vehicles; mid-file "point" updates for ids that no
        // longer exist would otherwise be ignored forever.
        foreach (TrajectoryCursor trajectory in trajectories)
            trajectory.Reset();
    }

    private void LoadTrajectories()
    {
        double south = double.MaxValue, west = double.MaxValue, north = double.MinValue, east = double.MinValue;
        for (int i = 0; i < TrajectoryCount; i++)
        {
            List<TrajectoryAction> actions = [];
            using StreamReader reader = new(Path.Combine(DemoParameters.TrajectoryPath, $"{i}.txt"));
            reader.ReadLine();
            reader.ReadLine();
            for (string? line = reader.ReadLine(); !string.IsNullOrEmpty(line); line = reader.ReadLine())
            {
                string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length != 5)
                    continue;

                // The data files use '.' decimals; parsing must not depend on the OS locale.
                double latitude = double.Parse(fields[3], CultureInfo.InvariantCulture);
                double longitude = double.Parse(fields[4], CultureInfo.InvariantCulture);
                south = Math.Min(south, latitude);
                west = Math.Min(west, longitude);
                north = Math.Max(north, latitude);
                east = Math.Max(east, longitude);
                actions.Add(new TrajectoryAction(
                    i,
                    ParseOperation(fields[2]),
                    new GeoPointShape(latitude, longitude)));
            }

            trajectories[i] = new TrajectoryCursor(actions);
        }

        TrajectorySouthWest = new GeoPoint(south, west);
        TrajectoryNorthEast = new GeoPoint(north, east);
    }

    private static TrajectoryOperation ParseOperation(string status) => status switch
    {
        _ when status.StartsWith('n') => TrajectoryOperation.Add,
        _ when status.StartsWith('p') => TrajectoryOperation.Move,
        _ when status.StartsWith('d') => TrajectoryOperation.Delete,
        _ => TrajectoryOperation.None,
    };

    /// <summary>
    /// Runs the replay ticks that are due, then waits for the next one. A DispatcherTimer fires
    /// only on the system timer's 15.6 ms grid, so a fixed 40 ms interval really ran every
    /// 46.9 ms (21 updates/s when 25 were asked for) and 20 ms every 31.2 ms (32 for 50). The
    /// replay keeps its own schedule instead: a late firing runs the tick it was late for, the
    /// next interval is shortened to make up for it, and the requested rate holds on average.
    /// </summary>
    private void OnTimer(object? sender, EventArgs e)
    {
        // The ticks' map commands go out as one message when the firing ends.
        map.BeginBatch();
        try
        {
            for (int ran = 0; clock.Elapsed >= nextTickDue; ran++)
            {
                if (ran == MaxTicksPerFiring)
                {
                    // Far behind (the UI thread was blocked): carry on from now rather than race.
                    nextTickDue = clock.Elapsed + tickPeriod;
                    break;
                }

                Tick();
                nextTickDue += tickPeriod;
            }
        }
        finally
        {
            map.EndBatch();
        }

        TimeSpan untilNext = nextTickDue - clock.Elapsed;
        ((DispatcherTimer)sender!).Interval = untilNext > TimeSpan.Zero ? untilNext : TimeSpan.Zero;
    }

    private void Tick()
    {
        long start = DemoMetrics.BeginTick();
        if (previousVehicleCount == -1)
            previousVehicleCount = DemoParameters.VehicleCount;

        for (int i = 0; i < DemoParameters.VehicleCount; i++)
        {
            if (trajectories[i].Next() is TrajectoryAction action)
                Apply(action);
        }

        // Vehicles beyond a lowered count disappear and their trajectories rewind.
        for (int i = DemoParameters.VehicleCount; i < previousVehicleCount; i++)
        {
            trajectories[i].Reset();
            Apply(new TrajectoryAction(i, TrajectoryOperation.Delete, null));
        }

        previousVehicleCount = DemoParameters.VehicleCount;
        WatchPendingPairs();
        DemoMetrics.EndTick(start);
    }

    private void WatchPendingPairs()
    {
        if (pendingPairs.Count == 0)
            return;

        // Only a vehicle that is not on the map yet keeps a pair pending; added, already
        // watched and same-vehicle pairs are settled.
        pendingPairs.RemoveAll(pair =>
            WatchPair(pair.First, pair.Second, pendingPairsQuery) != PairSubscriptionResult.NotOnMap);

        if (pendingPairs.Count == 0)
            PendingPairsWatched?.Invoke();
    }

    private void Apply(TrajectoryAction action)
    {
        switch (action.Operation)
        {
            case TrajectoryOperation.Add:
                AddVehicle(action.Id, action.Location!);
                break;
            case TrajectoryOperation.Move:
                MoveVehicle(action.Id, action.Location!);
                break;
            case TrajectoryOperation.Delete:
                RemoveVehicle(action.Id);
                break;
        }
    }

    private void AddVehicle(int id, GeoPointShape location)
    {
        // A trajectory can replay an add for a vehicle that already exists; treat it
        // as a position update instead of crashing on the duplicate dictionary key.
        if (vehicles.ContainsKey(id))
        {
            MoveVehicle(id, location);
            return;
        }

        MovingObject vehicle = new(id, map, lines, overlaps);
        vehicle.PushLocation(location);
        vehicles.Add(id, vehicle);
        foreach (DemoPolygon polygon in polygons.Polygons)
        {
            if (DemoParameters.ActiveScenario == Scenario.Intersect)
                vehicle.SubscribeIntersect(polygon);
            else if (DemoParameters.ActiveScenario == Scenario.Distance)
                vehicle.SubscribeDistance(polygon.Name, polygon.Geography);
            else if (DemoParameters.ActiveScenario == Scenario.RxRxIntersection)
                vehicle.SubscribeZoneOverlap(polygon);
        }
    }

    private void MoveVehicle(int id, GeoPointShape location)
    {
        if (vehicles.TryGetValue(id, out MovingObject? vehicle))
            vehicle.PushLocation(location);
    }

    private void RemoveVehicle(int id)
    {
        if (vehicles.Remove(id, out MovingObject? vehicle))
            vehicle.Remove();
    }

    private enum TrajectoryOperation
    {
        None,
        Add,
        Move,
        Delete,
    }

    private sealed record TrajectoryAction(int Id, TrajectoryOperation Operation, GeoPointShape? Location);

    /// <summary>Replays a vehicle's action list, looping past the initial "newpoint" action.</summary>
    private sealed class TrajectoryCursor(List<TrajectoryAction> actions)
    {
        private int current;

        public TrajectoryAction? Next()
        {
            if (actions.Count == 0)
                return null;

            // Wrap past the initial "newpoint" action, unless it is the only action.
            if (current >= actions.Count)
                current = actions.Count > 1 ? 1 : 0;

            return actions[current++];
        }

        public void Reset() => current = 0;
    }
}
