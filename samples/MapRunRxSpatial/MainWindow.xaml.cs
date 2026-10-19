using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
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
        // WPF bindings format with en-US unless told otherwise; follow the OS culture so
        // "Observe distance: {0:N0} m" groups digits the way the user expects.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
        InitializeComponent();
        map = new MapLibreMap(mapView);
        map.MapClick += OnMapClick;
        map.MapMouseMove += OnMapMouseMove;
        polygons = new PolygonManager(map);
        lines = new ConnectionLines(map);
        fleet = new MovingObjectManager(map, polygons, lines);
        ShowGuidance(DemoParameters.ActiveScenario);
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
        => DemoParameters.VehicleCount = (int)e.NewValue;

    private void OnVehicleSpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        DemoParameters.MovementIntervalMilliseconds = 1000 / (int)e.NewValue;

        // Fires during InitializeComponent (Minimum coerces Value) before the fleet exists.
        if (fleet is not null && !isPaused)
            fleet.RestartTraffic();
    }

    private void OnObserveDistanceChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => DemoParameters.ObserveDistance = e.NewValue;

    /// <summary>Both pair tabs share this handler; the active scenario picks the controls.</summary>
    private void OnWatchPairClick(object sender, RoutedEventArgs e)
    {
        (Slider first, Slider second, TextBlock status, bool drawConnection) = DemoParameters.ActiveScenario switch
        {
            Scenario.RxRxDistance => (TrackedPairSlider1, TrackedPairSlider2, TrackedPairStatus, false),
            Scenario.RxRxDistanceConnection => (ConnectionPairSlider1, ConnectionPairSlider2, ConnectionPairStatus, true),
            _ => throw new InvalidOperationException("Watch pair is only offered on the vehicle-pair tabs."),
        };

        int id1 = (int)first.Value;
        int id2 = (int)second.Value;
        status.Text = DescribePairSubscription(id1, id2, fleet.WatchPair(id1, id2, drawConnection));
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

        TrackedPairStatus.Text = string.Empty;
        ConnectionPairStatus.Text = string.Empty;

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
        ShowGuidance(scenario);

        fleet.RestartTraffic();
        fleet.UnsubscribeAll();
        fleet.ClearAllVehicles();
        polygons.ClearAllPolygons();
        lines.Clear();
        CancelAreaDrawing();
    }

    /// <summary>
    /// Shows the active tab's guidance and the backend its vehicles were built with. Call
    /// only after <see cref="RxSpatialOptions.PreferredDataStruct"/> is assigned.
    /// </summary>
    private void ShowGuidance(Scenario scenario)
    {
        GuidanceInsideArea.Visibility = VisibleWhen(scenario == Scenario.Intersect);
        GuidanceNearArea.Visibility = VisibleWhen(scenario == Scenario.Distance);
        GuidanceNearVehicle.Visibility = VisibleWhen(scenario == Scenario.RxRxDistance);
        GuidanceConvoyLink.Visibility = VisibleWhen(scenario == Scenario.RxRxDistanceConnection);

        string backend = DescribeBackend(RxSpatialOptions.Default.PreferredDataStruct);
        BackendText.Text = scenario == Scenario.Intersect
            ? $"Spatial index: {backend} (appsettings.json)"
            : $"Spatial index: {backend}";
    }

    // Collapsed, not Hidden: a hidden panel would still reserve its height.
    private static Visibility VisibleWhen(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private static string DescribeBackend(RxGeographyDataStruct backend) => backend switch
    {
        RxGeographyDataStruct.List => "List",
        RxGeographyDataStruct.HashMap => "Grid",
        RxGeographyDataStruct.RTree => "R*-tree",
        RxGeographyDataStruct.RumTree => "RUM-tree",
        _ => backend.ToString(),
    };

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
