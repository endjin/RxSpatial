using System.Windows;
using System.Windows.Controls;
using RxSpatial;

namespace MapRunRxSpatial;

/// <summary>The demo shell: a MapLibre map beside a per-scenario control panel.</summary>
public partial class MainWindow : Window
{
    private const string PreviewAreaShapeId = "tmp-area";

    private readonly MapLibreMap map;
    private readonly PolygonManager polygons;
    private readonly ConnectionLines lines;
    private readonly MovingObjectManager fleet;

    // The configured backend (RUM-tree by default) drives the intersect scenario; the
    // distance-style tabs fall back to the list backend because spatial pruning would
    // suppress their continuous distance streams.
    private readonly RxGeographyDataStruct configuredBackend = RxSpatialOptions.Default.PreferredDataStruct;

    private bool isDrawingArea;
    private GeoPoint? corner1;
    private GeoPoint? corner2;
    private bool isPaused;

    public MainWindow()
    {
        InitializeComponent();
        map = new MapLibreMap(mapView);
        map.MapClick += OnMapClick;
        map.MapMouseMove += OnMapMouseMove;
        polygons = new PolygonManager(map);
        lines = new ConnectionLines(map);
        fleet = new MovingObjectManager(map, polygons, lines);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await map.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The map view could not be initialised. The WebView2 Runtime is required "
                + "(preinstalled on Windows 11, otherwise see "
                + "https://developer.microsoft.com/microsoft-edge/webview2/).\n\n" + ex.Message,
                "MapRunRxSpatial", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void OnStartClick(object sender, RoutedEventArgs e) => fleet.StartTraffic();

    private void OnAddAreaClick(object sender, RoutedEventArgs e)
    {
        // Re-arming mid-draw must not leave the previous preview rectangle behind.
        map.RemoveShape(PreviewAreaShapeId);
        isDrawingArea = true;
        corner1 = null;
        corner2 = null;
    }

    /// <summary>Clears areas and watched pairs alike (Clear areas / Clear pairs).</summary>
    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        fleet.UnsubscribeAll();
        polygons.ClearAllPolygons();
        lines.Clear();
        CancelAreaDrawing();
    }

    private void OnPauseResumeClick(object sender, RoutedEventArgs e)
    {
        // Pause/Resume has nothing to act on until Start has been pressed.
        if (!fleet.TrafficStarted)
            return;

        if (isPaused)
            fleet.RestartTraffic();
        else
            fleet.StopTraffic();

        isPaused = !isPaused;
        SetPauseResumeLabels(isPaused ? "Resume" : "Pause");
    }

    private void OnMapClick(double latitude, double longitude)
    {
        if (!isDrawingArea)
            return;

        GeoPoint corner = new(latitude, longitude);
        if (corner1 is null)
        {
            corner1 = corner;
        }
        else if (corner2 is null)
        {
            corner2 = corner;
            map.RemoveShape(PreviewAreaShapeId);
            AddArea(corner1.Value, corner2.Value);
            isDrawingArea = false;
            corner1 = null;
            corner2 = null;
        }
    }

    private void OnMapMouseMove(double latitude, double longitude)
    {
        if (isDrawingArea && corner1 is not null)
            DrawAreaPreview(corner1.Value, new GeoPoint(latitude, longitude));
    }

    private void DrawAreaPreview(GeoPoint corner1, GeoPoint corner2)
        => map.SetLine(
            PreviewAreaShapeId,
            [
                new GeoPoint(corner1.Latitude, corner1.Longitude),
                new GeoPoint(corner1.Latitude, corner2.Longitude),
                new GeoPoint(corner2.Latitude, corner2.Longitude),
                new GeoPoint(corner2.Latitude, corner1.Longitude),
                new GeoPoint(corner1.Latitude, corner1.Longitude),
            ],
            "orange", 2, false, 0.7);

    private void AddArea(GeoPoint corner1, GeoPoint corner2)
    {
        DemoPolygon? polygon = polygons.AddPolygon(corner1, corner2);
        if (polygon is null)
            return;

        if (DemoParameters.ActiveScenario == Scenario.Intersect)
            fleet.SubscribeIntersectToNewPolygon(polygon.Name, polygon.Geography);
        else if (DemoParameters.ActiveScenario == Scenario.Distance)
            fleet.SubscribeDistanceToNewPolygon(polygon.Name, polygon.Geography);
    }

    private void OnVehicleCountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        DemoParameters.VehicleCount = (int)e.NewValue;
        SetLabels($" Vehicle Number: {(int)e.NewValue}", VehicleNum, VehicleNum1);
    }

    private void OnVehicleSpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        DemoParameters.MovementIntervalMilliseconds = 1000 / (int)e.NewValue;
        if (fleet is not null && !isPaused)
            fleet.RestartTraffic();

        SetLabels($" Vehicle Speed: {(int)e.NewValue}", VehicleSpeed, VehicleSpeed1);
        SetLabels($" Movement Speed: {(int)e.NewValue}", VehicleSpeed2, VehicleSpeed3);
    }

    private void OnObserveDistanceChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        DemoParameters.ObserveDistance = e.NewValue;
        SetLabels($" Observe Distance: {e.NewValue:F}", ObserveDist, ObserveDist1, ObserveDist2);
    }

    /// <summary>
    /// One handler serves the equivalent sliders on every tab, and a value coerced during
    /// InitializeComponent fires before the later tabs' labels exist — skip those.
    /// </summary>
    private static void SetLabels(string text, params TextBlock?[] labels)
    {
        foreach (TextBlock? label in labels)
        {
            if (label is not null)
                label.Text = text;
        }
    }

    private void OnTrackedPairChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => TrackedObserverState.Text =
            $"Observers:({(int)TrackedPairSlider1.Value},{(int)TrackedPairSlider2.Value}) SELECTED..";

    private void OnConnectionPairChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => ConnectionObserverState.Text =
            $"Observers:({(int)ConnectionPairSlider1.Value},{(int)ConnectionPairSlider2.Value}) SELECTED..";

    private void OnAddTrackedPairClick(object sender, RoutedEventArgs e)
    {
        int id1 = (int)TrackedPairSlider1.Value;
        int id2 = (int)TrackedPairSlider2.Value;
        TrackedObserverState.Text = DescribePairSubscription(
            id1, id2, fleet.WatchPair(id1, id2, drawConnection: false));
    }

    private void OnAddConnectionPairClick(object sender, RoutedEventArgs e)
    {
        int id1 = (int)ConnectionPairSlider1.Value;
        int id2 = (int)ConnectionPairSlider2.Value;
        ConnectionObserverState.Text = DescribePairSubscription(
            id1, id2, fleet.WatchPair(id1, id2, drawConnection: true));
    }

    private static string DescribePairSubscription(int id1, int id2, PairSubscriptionResult result) => result switch
    {
        PairSubscriptionResult.Added => $"Watching vehicles {id1} and {id2}.",
        PairSubscriptionResult.SameVehicle => "Choose two different vehicles.",
        PairSubscriptionResult.NotOnMap => $"Press Start first - vehicles {id1} and {id2} are not on the map yet.",
        PairSubscriptionResult.AlreadyWatched => $"Vehicles {id1} and {id2} are already being watched.",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown pair subscription result."),
    };

    private void OnScenarioTabChanged(object sender, SelectionChangedEventArgs e)
    {
        // Fires only on a real tab change (never for clicks inside the tab content, which
        // used to bubble to the old MouseLeftButtonUp handlers and wipe the demo state).
        // Skipped during InitializeComponent, before the managers exist.
        if (fleet is null || e.Source is not TabControl tabs)
            return;

        Scenario? scenario = tabs.SelectedIndex switch
        {
            0 => Scenario.Intersect,
            1 => Scenario.Distance,
            2 => Scenario.RxRxDistance,
            3 => Scenario.RxRxDistanceConnection,
            _ => null,
        };

        if (scenario is not null)
            ResetScenario(scenario.Value);
    }

    private void ResetScenario(Scenario scenario)
    {
        isPaused = false;
        SetPauseResumeLabels("Pause");

        VehicleNum.Text = " Vehicle Number: 1";
        VehicleSpeed.Text = " Vehicle Speed: 1";
        VehicleNum1.Text = " Vehicle Number: 1";
        VehicleSpeed1.Text = " Vehicle Speed: 1";
        ObserveDist.Text = " Observe Distance: 1";
        VehicleSpeed2.Text = " Movement Speed: 1";
        VehicleSpeed3.Text = " Movement Speed: 1";
        ObserveDist1.Text = " Observe Distance: 1";
        ObserveDist2.Text = " Observe Distance: 1";
        TrackedObserverState.Text = "Observer Condition:";
        ConnectionObserverState.Text = "Observer Condition:";

        foreach (Slider slider in (Slider[])
            [
                VehicleCountSlider, VehicleCountSlider1, VehicleSpeedSlider, VehicleSpeedSlider1,
                VehicleSpeedSlider2, VehicleSpeedSlider3, ObserveDistanceSlider, ObserveDistanceSlider1,
                ObserveDistanceSlider2, TrackedPairSlider1, TrackedPairSlider2, ConnectionPairSlider1,
                ConnectionPairSlider2,
            ])
        {
            slider.Value = slider.Minimum;
        }

        DemoParameters.MovementIntervalMilliseconds = 1000;
        DemoParameters.ObserveDistance = 1;
        DemoParameters.VehicleCount =
            scenario is Scenario.RxRxDistance or Scenario.RxRxDistanceConnection ? 20 : 1;
        DemoParameters.ActiveScenario = scenario;

        RxSpatialOptions.Default.PreferredDataStruct = scenario == Scenario.Intersect
            ? configuredBackend
            : RxGeographyDataStruct.List;

        fleet.RestartTraffic();
        fleet.UnsubscribeAll();
        fleet.ClearAllVehicles();
        polygons.ClearAllPolygons();
        lines.Clear();
        CancelAreaDrawing();
    }

    private void SetPauseResumeLabels(string label)
    {
        PauseResume.Content = label;
        PauseResume1.Content = label;
        PauseResume2.Content = label;
        PauseResume3.Content = label;
    }

    private void CancelAreaDrawing()
    {
        isDrawingArea = false;
        corner1 = null;
        corner2 = null;
        map.RemoveShape(PreviewAreaShapeId);
    }
}
