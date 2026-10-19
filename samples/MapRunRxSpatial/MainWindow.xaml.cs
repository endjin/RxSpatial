using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using RxSpatial;
using RxSpatial.SqlServer;

namespace MapRunRxSpatial;

/// <summary>The demo shell: a MapLibre map beside a per-scenario control panel.</summary>
public partial class MainWindow : Window
{
    private readonly MapLibreMap map;
    private readonly PolygonManager polygons;
    private readonly ConnectionLines lines;
    private readonly OverlapShapes overlaps;
    private readonly MovingObjectManager fleet;

    // The appsettings.json backend (RUM-tree by default) is the Geofence tab's starting and
    // reset index; its Spatial index buttons switch it live. The distance-style tabs always
    // use the list backend because spatial pruning would suppress their continuous distance
    // streams. So does Overlap: a grid or tree only notifies an observer whose zone bounds
    // meet the pushed reach, so a pair drifting apart would never hear "no overlap" (its
    // markers would fall back to Blue, not watched, instead of Gray) and its lens, like a
    // reach leaving a zone, would never get the empty result that removes it.
    private readonly RxGeographyDataStruct configuredBackend = RxSpatialOptions.Default.PreferredDataStruct;

    // The pressed area button while the map is in drawing mode; null when it is not.
    private ToggleButton? drawingButton;
    private bool isPaused;

    // Watch all pairs was pressed before the vehicles were on the map, so the status line
    // still promises the pairs; cleared once they are watched, by Clear pairs or by a reset.
    private bool allPairsPending;

    public MainWindow()
    {
        // WPF bindings format with en-US unless told otherwise; follow the OS culture so
        // "Observe distance: {0:N0} m" groups digits the way the user expects.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
        InitializeComponent();
        FitToWorkArea();
        map = new MapLibreMap(mapView);
        map.AreaDrawn += OnAreaDrawn;
        map.DrawCancelled += EndAreaDrawing;
        polygons = new PolygonManager(map);
        lines = new ConnectionLines(map);
        overlaps = new OverlapShapes(map);
        fleet = new MovingObjectManager(map, polygons, lines, overlaps);
        fleet.PendingPairsWatched += OnPendingPairsWatched;
        ApplyControlDefaults(DemoParameters.ActiveScenario);
        ShowGuidance(DemoParameters.ActiveScenario);
        AnnounceChanges(TrackedPairStatus);
        AnnounceChanges(ConnectionPairStatus);
        AnnounceChanges(OverlapPairStatus);
        AnnounceChanges(BackendText);
    }

    /// <summary>
    /// The status lines are polite live regions (WCAG 4.1.3): a text change is announced by
    /// screen readers without moving focus. WPF raises no live-region event by itself.
    /// </summary>
    private static void AnnounceChanges(TextBlock status)
        => DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock)).AddValueChanged(
            status,
            (_, _) => UIElementAutomationPeer.FromElement(status)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged));

    /// <summary>
    /// The XAML size is a preference: shrink it to the primary screen's work area so a small
    /// or high-scaling display never pushes the window (and its guidance) off-screen.
    /// MinWidth/MinHeight still win, and the guidance panel scrolls below them.
    /// </summary>
    private void FitToWorkArea()
    {
        Rect workArea = SystemParameters.WorkArea;
        Width = Math.Min(Width, workArea.Width);
        Height = Math.Min(Height, workArea.Height);
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

    /// <summary>
    /// Restricted area (Geofence) and Add area (Near area) pressed: draw a restricted area.
    /// Checked/Unchecked rather than Click, so keyboard and UI Automation toggles work too.
    /// </summary>
    private void OnRestrictedAreaChecked(object sender, RoutedEventArgs e) => BeginAreaDrawing((ToggleButton)sender, AreaKind.Restricted);

    /// <summary>Confinement area (Geofence only) pressed: draw a confinement area.</summary>
    private void OnConfinementAreaChecked(object sender, RoutedEventArgs e) => BeginAreaDrawing((ToggleButton)sender, AreaKind.Confinement);

    /// <summary>Add zone (Overlap only) pressed: draw a zone.</summary>
    private void OnZoneChecked(object sender, RoutedEventArgs e) => BeginAreaDrawing((ToggleButton)sender, AreaKind.Zone);

    /// <summary>
    /// An area button released. The user releasing the pressed button cancels drawing; a
    /// release made by <see cref="EndAreaDrawing"/> or a take-over arrives after
    /// <see cref="drawingButton"/> has moved on, so it is ignored.
    /// </summary>
    private void OnAreaButtonUnchecked(object sender, RoutedEventArgs e)
    {
        if (sender == drawingButton)
            CancelAreaDrawing();
    }

    private void BeginAreaDrawing(ToggleButton button, AreaKind kind)
    {
        ToggleButton? previous = drawingButton;
        drawingButton = button;
        previous?.IsChecked = false;
        map.BeginDraw(kind);
    }

    /// <summary>
    /// Esc while the WPF panel has keyboard focus. When the map has focus, the WebView2 child
    /// window gets the key instead and map.html cancels (reported back as DrawCancelled).
    /// </summary>
    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && drawingButton is not null)
        {
            CancelAreaDrawing();
            e.Handled = true;
        }
    }

    private void OnAreaDrawn(AreaKind kind, GeoPoint corner1, GeoPoint corner2)
    {
        // A rectangle finished just as the mode was cancelled here is dropped.
        if (drawingButton is null)
            return;

        EndAreaDrawing();
        AddArea(corner1, corner2, kind);
    }

    /// <summary>Leaves drawing mode in the map and in the panel.</summary>
    private void CancelAreaDrawing()
    {
        map.CancelDraw();
        EndAreaDrawing();
    }

    /// <summary>Un-presses the area button; the map has left (or is leaving) drawing mode.</summary>
    private void EndAreaDrawing()
    {
        // Cleared first, so the Unchecked this raises is not taken for a user cancel.
        ToggleButton? button = drawingButton;
        drawingButton = null;
        button?.IsChecked = false;
    }

    /// <summary>Clear areas / Clear zones: removes every area and its queries; watched pairs stay.</summary>
    private void OnClearAreasClick(object sender, RoutedEventArgs e)
    {
        fleet.UnsubscribeAllAreas();
        polygons.ClearAllPolygons();
        CancelAreaDrawing();
    }

    /// <summary>Clear pairs: stops watching every pair, with its line or lens; areas stay.</summary>
    private void OnClearPairsClick(object sender, RoutedEventArgs e)
    {
        // Also drops the tab's pre-set pairs; they come back only on the next tab reset.
        fleet.UnwatchAllPairs();
        allPairsPending = false;
        TrackedPairStatus.Text = string.Empty;
        ConnectionPairStatus.Text = string.Empty;
        OverlapPairStatus.Text = string.Empty;
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

    private void AddArea(GeoPoint corner1, GeoPoint corner2, AreaKind kind)
    {
        DemoPolygon? polygon = polygons.AddPolygon(corner1, corner2, kind);
        if (polygon is null)
            return;

        if (DemoParameters.ActiveScenario == Scenario.Intersect)
            fleet.SubscribeIntersectToNewPolygon(polygon);
        else if (DemoParameters.ActiveScenario == Scenario.Distance)
            fleet.SubscribeDistanceToNewPolygon(polygon.Name, polygon.Geography);
        else if (DemoParameters.ActiveScenario == Scenario.RxRxIntersection)
            fleet.SubscribeZoneOverlapToNewPolygon(polygon);
    }

    private void OnVehicleCountChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => DemoParameters.VehicleCount = (int)e.NewValue;

    private void OnVehicleSpeedChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        DemoParameters.MovementIntervalMilliseconds = DemoParameters.ReplayIntervalMilliseconds((int)e.NewValue);

        // Fires during InitializeComponent (Minimum coerces Value) before the fleet exists.
        if (fleet is not null && !isPaused)
            fleet.RestartTraffic();
    }

    private void OnObserveDistanceChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => DemoParameters.ObserveDistance = e.NewValue;

    /// <summary>Overlap only: the next push of every vehicle uses the new reach.</summary>
    private void OnReachChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => DemoParameters.Reach = e.NewValue;

    /// <summary>
    /// The pair tabs share this handler; the active scenario picks the controls. A pair is
    /// watched with the query its tab's pre-set pairs use.
    /// </summary>
    private void OnWatchPairClick(object sender, RoutedEventArgs e)
    {
        (Slider first, Slider second) = DemoParameters.ActiveScenario switch
        {
            Scenario.RxRxDistance => (TrackedPairSlider1, TrackedPairSlider2),
            Scenario.RxRxDistanceConnection => (ConnectionPairSlider1, ConnectionPairSlider2),
            Scenario.RxRxIntersection => (OverlapPairSlider1, OverlapPairSlider2),
            _ => throw new InvalidOperationException("Watch pair is only offered on the vehicle-pair tabs."),
        };

        int id1 = (int)first.Value;
        int id2 = (int)second.Value;
        PairStatus(DemoParameters.ActiveScenario).Text =
            DescribePairSubscription(id1, id2, fleet.WatchPair(id1, id2, PairQueryFor(DemoParameters.ActiveScenario)));
    }

    /// <summary>
    /// Near vehicle and Overlap: the self join over every pair. It is watched without lines or
    /// lenses: on Overlap, 190 lenses would be computed twice per tick and cover the map.
    /// </summary>
    private void OnWatchAllPairsClick(object sender, RoutedEventArgs e)
    {
        PairQuery query = DemoParameters.ActiveScenario == Scenario.RxRxIntersection ? PairQuery.Overlap : PairQuery.Distance;
        int waiting = fleet.WatchAllPairs(query);
        allPairsPending = waiting > 0;
        PairStatus(DemoParameters.ActiveScenario).Text = waiting switch
        {
            0 => AllPairsWatchedText,
            _ when !fleet.TrafficStarted => $"All {MovingObjectManager.AllPairsCount} pairs will be watched from Start.",
            _ => $"All {MovingObjectManager.AllPairsCount} pairs will be watched as soon as their vehicles are on the map.",
        };
    }

    private static string AllPairsWatchedText => $"Watching all {MovingObjectManager.AllPairsCount} pairs.";

    /// <summary>Replaces the "will be watched" promise once the queued all-pairs request is met.</summary>
    private void OnPendingPairsWatched()
    {
        if (!allPairsPending)
            return;

        allPairsPending = false;
        PairStatus(DemoParameters.ActiveScenario).Text = AllPairsWatchedText;
    }

    /// <summary>The status line under a pair tab's Watch pair button.</summary>
    private TextBlock PairStatus(Scenario scenario) => scenario switch
    {
        Scenario.RxRxDistance => TrackedPairStatus,
        Scenario.RxRxDistanceConnection => ConnectionPairStatus,
        Scenario.RxRxIntersection => OverlapPairStatus,
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Only the vehicle-pair tabs watch pairs."),
    };

    /// <summary>The query a tab watches its pre-set and Watch pair pairs with.</summary>
    private static PairQuery PairQueryFor(Scenario scenario) => scenario switch
    {
        Scenario.RxRxDistanceConnection => PairQuery.DistanceWithConnection,
        Scenario.RxRxIntersection => PairQuery.OverlapWithLens,
        _ => PairQuery.Distance,
    };

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
            4 => Scenario.RxRxIntersection,
            _ => null,
        };

        if (scenario is not null)
            ResetScenario(scenario.Value);
    }

    private void ResetScenario(Scenario scenario)
    {
        // Overlap zooms to the trajectories: at the default view a 2 km reach is barely wider
        // than a marker, and the lenses would hide under the markers. Leaving it restores the
        // default view; switches between the other tabs keep the user's view, as before.
        if (scenario == Scenario.RxRxIntersection)
            map.FitBounds(fleet.TrajectorySouthWest, fleet.TrajectoryNorthEast);
        else if (DemoParameters.ActiveScenario == Scenario.RxRxIntersection)
            map.ResetView();

        isPaused = false;
        allPairsPending = false;
        SetPauseResumeLabels("Pause");

        ApplyControlDefaults(scenario);

        RxSpatialOptions.Default.PreferredDataStruct = scenario == Scenario.Intersect
            ? configuredBackend
            : RxGeographyDataStruct.List;
        ShowGuidance(scenario);

        fleet.RestartTraffic();
        fleet.UnsubscribeAll();
        fleet.ClearAllVehicles();
        polygons.ClearAllPolygons();
        lines.Clear();
        overlaps.Clear();
        CancelAreaDrawing();

        // Watched from the first tick that puts both vehicles on the map.
        fleet.WatchPairsWhenOnMap(DemoParameters.DefaultPairs(scenario), PairQueryFor(scenario));
    }

    /// <summary>
    /// Sets every tab's controls and the shared demo state to the scenario's defaults, taken
    /// from the papers' figures (see <see cref="DemoParameters"/>). Safe to call from the
    /// constructor: the slider handlers it triggers tolerate traffic that has not started.
    /// </summary>
    private void ApplyControlDefaults(Scenario scenario)
    {
        IReadOnlyList<VehiclePair> nearVehiclePairs = DemoParameters.DefaultPairs(Scenario.RxRxDistance);
        IReadOnlyList<VehiclePair> convoyPairs = DemoParameters.DefaultPairs(Scenario.RxRxDistanceConnection);
        IReadOnlyList<VehiclePair> overlapPairs = DemoParameters.DefaultPairs(Scenario.RxRxIntersection);

        VehicleCountSlider.Value = DemoParameters.DefaultVehicleCount;
        VehicleCountSlider1.Value = DemoParameters.DefaultVehicleCount;
        foreach (Slider slider in (Slider[])[VehicleSpeedSlider, VehicleSpeedSlider1, VehicleSpeedSlider2, VehicleSpeedSlider3, VehicleSpeedSlider4])
            slider.Value = DemoParameters.DefaultReplayRate;

        ObserveDistanceSlider.Value = DemoParameters.DefaultObserveDistance(Scenario.Distance);
        ObserveDistanceSlider1.Value = DemoParameters.DefaultObserveDistance(Scenario.RxRxDistance);
        ObserveDistanceSlider2.Value = DemoParameters.DefaultObserveDistance(Scenario.RxRxDistanceConnection);
        (TrackedPairSlider1.Value, TrackedPairSlider2.Value) = (nearVehiclePairs[0].First, nearVehiclePairs[0].Second);
        (ConnectionPairSlider1.Value, ConnectionPairSlider2.Value) = (convoyPairs[0].First, convoyPairs[0].Second);
        (OverlapPairSlider1.Value, OverlapPairSlider2.Value) = (overlapPairs[0].First, overlapPairs[0].Second);
        ReachSlider.Value = DemoParameters.DefaultReach;

        TrackedPairStatus.Text = DescribePresetPairs(nearVehiclePairs);
        ConnectionPairStatus.Text = DescribePresetPairs(convoyPairs);
        OverlapPairStatus.Text = DescribePresetPairs(overlapPairs);

        // The slider handlers above overwrite the shared state in whichever order they fire,
        // so assign it explicitly last.
        DemoParameters.MovementIntervalMilliseconds = DemoParameters.ReplayIntervalMilliseconds(DemoParameters.DefaultReplayRate);
        DemoParameters.ObserveDistance = DemoParameters.DefaultObserveDistance(scenario);
        DemoParameters.Reach = DemoParameters.DefaultReach;
        DemoParameters.VehicleCount = DemoParameters.DefaultVehicleCount;
        DemoParameters.ActiveScenario = scenario;

        // After ActiveScenario: OnSpatialIndexChecked acts only on the Geofence tab, and
        // ResetScenario assigns the backend itself.
        SpatialIndexButton(configuredBackend).IsChecked = true;
    }

    private RadioButton SpatialIndexButton(RxGeographyDataStruct backend) => backend switch
    {
        RxGeographyDataStruct.List => IndexList,
        RxGeographyDataStruct.HashMap => IndexGrid,
        RxGeographyDataStruct.RTree => IndexRTree,
        RxGeographyDataStruct.RumTree => IndexRumTree,
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown spatial index."),
    };

    /// <summary>
    /// A Geofence Spatial index button checked: rebuilds the vehicles on the new index (the
    /// backend is baked into each RxSqlGeography at construction). The drawn areas stay, and
    /// each recreated vehicle subscribes to them as it reappears on the next traffic tick;
    /// area drawing and the traffic timer are left alone.
    /// </summary>
    private void OnSpatialIndexChecked(object sender, RoutedEventArgs e)
    {
        // The constructor and tab resets check a button too (see ApplyControlDefaults); only a
        // real change on the Geofence tab rebuilds the fleet. XAML loading checks none, but the
        // managers are guarded all the same, as in the slider handlers.
        RxGeographyDataStruct backend = (RxGeographyDataStruct)((RadioButton)sender).Tag;
        if (fleet is null
            || DemoParameters.ActiveScenario != Scenario.Intersect
            || backend == RxSpatialOptions.Default.PreferredDataStruct)
            return;

        RxSpatialOptions.Default.PreferredDataStruct = backend;
        ShowBackend();
        fleet.ClearAllVehicles();
    }

    private static string DescribePresetPairs(IReadOnlyList<VehiclePair> pairs)
        => "Pre-set pairs, watched from Start: "
            + string.Join(", ", pairs.Select(pair => FormattableString.Invariant($"{pair.First}-{pair.Second}")))
            + ".";

    /// <summary>
    /// Shows the active tab's guidance and the backend its vehicles were built with. Call
    /// only after <see cref="RxSpatialOptions.PreferredDataStruct"/> is assigned.
    /// </summary>
    private void ShowGuidance(Scenario scenario)
    {
        GuidanceGeofence.Visibility = VisibleWhen(scenario == Scenario.Intersect);
        GuidanceNearArea.Visibility = VisibleWhen(scenario == Scenario.Distance);
        GuidanceNearVehicle.Visibility = VisibleWhen(scenario == Scenario.RxRxDistance);
        GuidanceConvoyLink.Visibility = VisibleWhen(scenario == Scenario.RxRxDistanceConnection);
        GuidanceOverlap.Visibility = VisibleWhen(scenario == Scenario.RxRxIntersection);

        ShowBackend();
    }

    /// <summary>Shows the backend the vehicles are built with in the guidance footer.</summary>
    private void ShowBackend()
        => BackendText.Text = $"Spatial index: {DescribeBackend(RxSpatialOptions.Default.PreferredDataStruct)}";

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
        PauseResume4.Content = label;
    }
}
