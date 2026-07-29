using System.Globalization;
using System.IO;
using System.Windows.Threading;
using Microsoft.SqlServer.Types;

namespace MapRunRxSpatial;

/// <summary>
/// Replays the recorded vehicle trajectories on a timer and keeps the fleet subscribed to
/// the active scenario's continuous queries.
/// </summary>
internal sealed class MovingObjectManager
{
    private const int TrajectoryCount = 20;

    private readonly Dictionary<int, MovingObject> vehicles = [];
    private readonly TrajectoryCursor[] trajectories = new TrajectoryCursor[TrajectoryCount];
    private readonly MapLibreMap map;
    private readonly PolygonManager polygons;
    private DispatcherTimer? timer;
    private int previousVehicleCount = -1;

    public MovingObjectManager(MapLibreMap map, PolygonManager polygons)
    {
        this.map = map;
        this.polygons = polygons;
        LoadTrajectories();
    }

    public bool TrafficStarted { get; private set; }

    public void StartTraffic()
    {
        // Stop any running timer first: repeated Start clicks must not stack timers
        // (each stacked timer would multiply the traffic speed).
        StopTraffic();
        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DemoParameters.MovementIntervalMilliseconds),
        };
        timer.Tick += OnTick;
        timer.Start();
        TrafficStarted = true;
    }

    public void StopTraffic() => timer?.Stop();

    public void RestartTraffic()
    {
        if (TrafficStarted)
            StartTraffic();
    }

    public void SubscribeIntersectToNewPolygon(string name, SqlGeography polygon)
    {
        foreach (MovingObject vehicle in vehicles.Values)
            vehicle.SubscribeIntersect(name, polygon);
    }

    public void SubscribeDistanceToNewPolygon(string name, SqlGeography polygon)
    {
        foreach (MovingObject vehicle in vehicles.Values)
            vehicle.SubscribeDistance(name, polygon);
    }

    public bool SubscribeRxRxDistance(int id1, int id2)
    {
        // Membership checks rather than count-based bounds: ids need not stay
        // contiguous if a trajectory deletes a vehicle mid-playback.
        return vehicles.TryGetValue(id1, out MovingObject? first)
            && vehicles.TryGetValue(id2, out MovingObject? second)
            && first.SubscribeRxRxDistance(SubscriptionKey(id2), second.Geography);
    }

    public bool SubscribeRxRxDistanceConnection(int id1, int id2)
    {
        return vehicles.TryGetValue(id1, out MovingObject? first)
            && vehicles.TryGetValue(id2, out MovingObject? second)
            && first.SubscribeRxRxDistanceConnection(SubscriptionKey(id2), second.Geography);
    }

    /// <summary>Subscriptions are named after the tracked vehicle's id, locale-independently.</summary>
    private static string SubscriptionKey(int id) => id.ToString(CultureInfo.InvariantCulture);

    public void UnsubscribeAll()
    {
        foreach (MovingObject vehicle in vehicles.Values)
            vehicle.UnsubscribeAll();
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
                actions.Add(new TrajectoryAction(
                    i,
                    ParseOperation(fields[2]),
                    SqlGeography.Point(latitude, longitude, 4326)));
            }

            trajectories[i] = new TrajectoryCursor(actions);
        }
    }

    private static TrajectoryOperation ParseOperation(string status) => status switch
    {
        _ when status.StartsWith('n') => TrajectoryOperation.Add,
        _ when status.StartsWith('p') => TrajectoryOperation.Move,
        _ when status.StartsWith('d') => TrajectoryOperation.Delete,
        _ => TrajectoryOperation.None,
    };

    private void OnTick(object? sender, EventArgs e)
    {
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

    private void AddVehicle(int id, SqlGeography location)
    {
        // A trajectory can replay an add for a vehicle that already exists; treat it
        // as a position update instead of crashing on the duplicate dictionary key.
        if (vehicles.ContainsKey(id))
        {
            MoveVehicle(id, location);
            return;
        }

        MovingObject vehicle = new(id, map, polygons);
        vehicle.PushLocation(location);
        vehicles.Add(id, vehicle);
        foreach (DemoPolygon polygon in polygons.Polygons)
        {
            if (DemoParameters.ActiveScenario == Scenario.Intersect)
                vehicle.SubscribeIntersect(polygon.Name, polygon.Geography);
            else if (DemoParameters.ActiveScenario == Scenario.Distance)
                vehicle.SubscribeDistance(polygon.Name, polygon.Geography);
        }
    }

    private void MoveVehicle(int id, SqlGeography location)
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

    private sealed record TrajectoryAction(int Id, TrajectoryOperation Operation, SqlGeography? Location);

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
