---
paths:
  - "solutions/MapRunRxSpatial/**"
---

# WPF demo (`solutions/MapRunRxSpatial`)

WPF + WebView2 hosting MapLibre GL JS (`wwwroot/map.html`); `MapLibreMap` is the C#↔page bridge.
Five tabs: Geofence, Near area, Near vehicle, Convoy link, Overlap (mapped to scenarios by index).

## Runtime model
- Requires `IsVisualized=true` (WebView2 must be driven from the UI thread; buffered mode
  notifies from the pool) — `App.OnStartup` forces it over appsettings.json.
- `RxGeography` notifies location observers before zone observers: `VehicleMarker.MoveTo` resets
  the marker to Blue and query observers may only *raise* it (`VehicleColor` is urgency-ordered).
  Bands come from `DistanceBands`; pairs go red below 1/7 of the observe distance; NaN → OutOfRange.
- RxRx observers fire only when their *own* vehicle moves, so both directions of a pair are
  subscribed; connection lines are keyed by `VehiclePair` (one line per pair).
- Confinement breach ("outside every confinement area") is decided at the end of
  `MovingObject.PushLocation`, not by an inverted observer: tree/grid backends never notify a
  zone the vehicle is far outside. Relies on synchronous notification inside `OnNext`.
- Overlap tab: pushes `new GeoCircle(position, Reach)` (marker from the `VehicleFix` alongside),
  uses the List backend (trees would prune the "no longer overlapping" notification), and
  upserts lenses/zone overlaps per pair/zone via `OverlapShapes`.
- The Geofence spatial-index switch sets `RxSpatialOptions.Default.PreferredDataStruct` and
  calls `ClearAllVehicles()` (the backend is baked into each `RxGeography` at construction).
- `GeoPoint` validates its range: keep sentinels (e.g. `double.MaxValue`) in plain doubles.

## Map bridge (performance rules — see docs/performance.md §5–6)
- One web message per dispatcher operation: `MapLibreMap` queues `MapCommand`s and posts one
  JSON array after the handler returns. Never post per command. The replay timer brackets its
  ticks with `BeginBatch`/`EndBatch` and flushes directly (no `BeginInvoke` per tick); other
  callers get a flush scheduled with `BeginInvoke`. `CoreWebView2` is cached in a field. `MarkerCommand` sends only
  changed fields; outlines travel as integer micro-degree deltas (`OutlineWriter` ↔ `decodeRing`).
- map.html caps data-driven repaints at 60/s (full rate while the map moves). Render-bound costs
  scale with display refresh rate (240 Hz here) — compare runs back to back only.
- The replay keeps its own `Stopwatch` schedule; `DispatcherTimer` alone fires on the 15.6 ms tick.
- Memory: the panel renders in software (`RenderOptions.ProcessRenderMode`, App.OnStartup) — a
  D3D9 swap chain for a 4K window cost ~70 MB private + GPU memory; don't revert without
  measuring. V8's young generation is capped via `MapLibreMap.BrowserArguments`
  (`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` is merged, not replaced). Minimising hides the
  WebView2 and lowers its memory target.

## UI conventions
- Theme: Fluent light via `ThemeMode="Light"` in App.xaml (keep it in XAML — no WPF0001
  suppression needed). Keyed styles must be `BasedOn` the Fluent implicit style or they fall back
  to grey classic controls. The TabControl template must name `PART_SelectedContentHost`.
- Contrast floor is WCAG 2.2 AA. Never use `TextFillColorTertiaryBrush` for meaningful text; WPF
  renders translucent text lighter than its composited colour, so `AccessibleTheme` swaps the
  secondary brush for opaque #5C5C5C and pins the accent when it would fail. Measure contrast
  from PrintWindow pixels.
- Every marker state needs a unique shape (WCAG 1.4.1) — blue circle, gray dashed ring, green
  rounded square, yellow triangle, red diamond — kept in step across `VehicleColors`,
  map.html `MARKER_SHAPES` + CSS, and all five legends. A new state needs a new shape.
- Tab header panel stays `TabNavigation="Once"` (focusing a TabItem resets the scenario). Five
  headers fit the panel only at 13px/6px padding; a sixth tab needs a different strip.
- Shared slider handlers fire during InitializeComponent before later tabs' elements exist:
  cross-tab control access must be null-tolerant.
- Area drawing lives in map.html (`beginDraw`/`cancelDraw` in, `areaDrawn`/`drawCancelled` out);
  Esc is handled in both focus domains (WebView2 and WPF), idempotently. Area buttons are
  ToggleButtons on Checked/Unchecked; `EndAreaDrawing` clears `drawingButton` before un-checking.
  Area styles are defined once in `AREA_STYLES`. Measure pointer positions against
  `map.getContainer()` (the canvas container has zero height).
- MapLibre zoom uses the 512px-tile convention (one level lower than Bing/Leaflet).

## Verifying UI changes
- Drive the app with UI Automation; draw via posted `WM_MOUSEMOVE`/`WM_LBUTTONDOWN` to
  `Chrome_RenderWidgetHostHWND` (never move the real cursor); capture with PrintWindow (never
  screen capture). Existing check scripts are a timing-flaky known issue (backlog).
- Metrics: `dotnet-counters collect -n MapRunRxSpatial --counters System.Runtime,MapRunRxSpatial`.
  Measure UI-thread CPU with cycle counters (`QueryThreadCycleTime`), not TotalProcessorTime.
  Never take a full `dotnet-dump` before a memory measurement (it inflates the working set).
