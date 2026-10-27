// Copyright (c) Endjin Limited. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using RxSpatial.Geo;

namespace MapRunRxSpatial;

/// <summary>
/// A thin C# facade over MapLibre GL JS running in a WebView2 (see wwwroot/map.html).
/// Commands are batched: everything sent during one dispatcher operation (a traffic tick, a
/// click) is posted as a single JSON web message, an array of commands, once that operation
/// has finished. Within a batch, updates to the same marker, line or overlay are merged, so
/// the map sees only their final state. Map events (ready, areaDrawn, drawCancelled) come back
/// as web messages. Commands sent before the map finishes loading wait in the batch.
/// All members must be used from the UI thread (a WebView2 requirement).
/// </summary>
/// <remarks>
/// One message per tick rather than one per command: each web message costs an IPC hop, a
/// message event and a JSON parse in the page, and a tick used to send 25-70 of them.
/// </remarks>
internal sealed class MapLibreMap
{
    /// <summary>
    /// Caps V8's young generation (each semi-space) at 2 MB, in the page and in MapLibre's
    /// worker. The page builds fresh GeoJSON for every overlay source on every frame, and V8
    /// sized the young generation for that short-lived garbage (page heap ~63 MB for ~22 MB
    /// live): the renderer process sat at ~200 MB on the Overlap and Convoy link tabs, ~100 MB
    /// with the cap, for the same renderer CPU (scavenges stay cheap; little survives them).
    /// 1 MB saved no more and cost renderer CPU.
    /// </summary>
    /// <remarks>
    /// WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS (the perf harness sets a remote-debugging port) is
    /// merged with these arguments, not replaced by them: both reach the browser process.
    /// </remarks>
    private const string BrowserArguments = "--js-flags=--max-semi-space-size=2";

    private readonly WebView2 webView;

    // The batch in send order. An upsert of something already in the batch replaces its entry
    // in place; a removal clears the entry (null) and is appended, so the commands for any one
    // id keep their order.
    private readonly List<MapCommand?> batch = [];
    private readonly Dictionary<CommandKey, int> batchIndex = [];
    private readonly Dictionary<string, MarkerCommand> markers = [];
    private readonly OutlineWriter outlines = new();
    private readonly ArrayBufferWriter<byte> json = new(16 * 1024);
    private readonly Action flush;
    private bool flushScheduled;
    private bool ready;

    public MapLibreMap(WebView2 webView)
    {
        this.webView = webView;
        flush = Flush;
    }

    /// <summary>
    /// Raised with the kind and two opposite corners (south-west, north-east) when the user
    /// finishes dragging a rectangle in drawing mode. The map has already left drawing mode.
    /// </summary>
    public event Action<AreaKind, GeoPoint, GeoPoint>? AreaDrawn;

    /// <summary>Raised when the user cancels drawing mode in the map (Esc or a right-click).</summary>
    public event Action? DrawCancelled;

    private enum CommandKind
    {
        Marker,
        Shape,
        Lens,
        ZoneOverlap,
    }

    public async Task InitializeAsync()
    {
        CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: null,
            new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = BrowserArguments });
        await webView.EnsureCoreWebView2Async(environment);
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "maprun.local",
            Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            CoreWebView2HostResourceAccessKind.Deny);
        webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        webView.CoreWebView2.Navigate("https://maprun.local/map.html");
    }

    /// <summary>
    /// Hides the map while the window is minimised, and asks WebView2 to keep its memory low.
    /// A minimised window does not hide a WebView2, so the page went on drawing the map at the
    /// display rate (renderer ~45%, GPU process ~35% of a core on the Overlap tab) with its
    /// memory resident. Hidden, the page stops drawing and Chromium releases its frame
    /// resources; the low memory target trims the processes' working sets (~535 to ~105 MB on
    /// the Overlap tab). Commands keep flowing, so the map is current when it is shown again.
    /// </summary>
    public void SetMinimized(bool minimized)
    {
        webView.Visibility = minimized ? Visibility.Hidden : Visibility.Visible;
        if (webView.CoreWebView2 is { } core)
        {
            core.MemoryUsageTargetLevel = minimized
                ? CoreWebView2MemoryUsageTargetLevel.Low
                : CoreWebView2MemoryUsageTargetLevel.Normal;
        }
    }

    /// <summary>
    /// Places (or moves) a vehicle marker. A non-null <paramref name="reach"/> also draws the
    /// vehicle's reach circle (the Overlap tab); null removes it. The circle's outline comes
    /// from <see cref="GeoOperations.ToVertices(GeoShape, Span{GeoPoint}, int)"/>, so it is exactly the disc the queries use.
    /// </summary>
    public void SetMarker(string id, string label, GeoPoint position, MarkerStyle style, GeoCircle? reach)
    {
        if (!markers.TryGetValue(id, out MarkerCommand? marker))
        {
            marker = new MarkerCommand(id, label);
            markers.Add(id, marker);
        }

        marker.Move(position, style, reach);
        Upsert(new CommandKey(CommandKind.Marker, id), marker);
    }

    public void SetMarkerColor(string id, MarkerStyle style)
    {
        // A marker not on the map has no colour to change (map.html ignored it too).
        if (!markers.TryGetValue(id, out MarkerCommand? marker))
            return;

        marker.Style = style;
        Upsert(new CommandKey(CommandKind.Marker, id), marker);
    }

    public void RemoveMarker(string id)
    {
        markers.Remove(id);
        Remove(new CommandKey(CommandKind.Marker, id), new { cmd = "removeMarker", id });
    }

    public void SetLine(string id, GeoPoint from, GeoPoint to, string color, double width, bool dashed, double opacity)
        => Upsert(new CommandKey(CommandKind.Shape, id), new LineCommand(id, from, to, color, width, dashed, opacity));

    /// <summary>
    /// Draws a finished area between two opposite corners, styled by its kind (the styles live
    /// in map.html, shared with the drawing preview), with a label in its top-left corner.
    /// </summary>
    public void SetArea(string id, AreaKind kind, GeoPoint corner1, GeoPoint corner2)
        => Send(new
        {
            cmd = "setArea", id, kind = KindName(kind),
            south = Math.Min(corner1.Latitude, corner2.Latitude),
            west = Math.Min(corner1.Longitude, corner2.Longitude),
            north = Math.Max(corner1.Latitude, corner2.Latitude),
            east = Math.Max(corner1.Longitude, corner2.Longitude),
        });

    /// <summary>Removes a line or an area (and the area's label).</summary>
    public void RemoveShape(string id)
        => Remove(new CommandKey(CommandKind.Shape, id), new { cmd = "removeShape", id });

    /// <summary>
    /// Enters drawing mode: a left drag draws a rectangle of the given kind instead of panning.
    /// The mode ends with <see cref="AreaDrawn"/>, <see cref="DrawCancelled"/> or <see cref="CancelDraw"/>.
    /// </summary>
    public void BeginDraw(AreaKind kind)
        => Send(new { cmd = "beginDraw", kind = KindName(kind) });

    /// <summary>Leaves drawing mode, if it is on, without raising <see cref="DrawCancelled"/>.</summary>
    public void CancelDraw()
        => Send(new { cmd = "cancelDraw" });

    /// <summary>Zooms and pans the map so the rectangle between two opposite corners fills it.</summary>
    public void FitBounds(GeoPoint corner1, GeoPoint corner2)
        => Send(new
        {
            cmd = "fitBounds",
            south = Math.Min(corner1.Latitude, corner2.Latitude),
            west = Math.Min(corner1.Longitude, corner2.Longitude),
            north = Math.Max(corner1.Latitude, corner2.Latitude),
            east = Math.Max(corner1.Longitude, corner2.Longitude),
        });

    /// <summary>Returns the map to its start-up view (DEFAULT_VIEW in map.html).</summary>
    public void ResetView()
        => Send(new { cmd = "resetView" });

    /// <summary>
    /// Draws (or redraws) a query-result region styled by its kind (OVERLAY_STYLES in map.html)
    /// from the outline of <paramref name="region"/>, a <see cref="GeoPolygon"/> or
    /// <see cref="GeoCircle"/> (one convex ring). A non-null <paramref name="label"/> is shown
    /// in a pill beside the region, clear of the markers.
    /// </summary>
    public void SetOverlay(OverlayKind kind, string id, GeoShape region, string? label)
        => Upsert(new CommandKey(OverlayCommandKind(kind), id), new OverlayCommand(OverlayName(kind), id, region, label));

    public void RemoveOverlay(OverlayKind kind, string id)
        => Remove(new CommandKey(OverlayCommandKind(kind), id), new { cmd = "removeOverlay", kind = OverlayName(kind), id });

    private static string KindName(AreaKind kind) => kind switch
    {
        AreaKind.Restricted => "restricted",
        AreaKind.Confinement => "confinement",
        AreaKind.Zone => "zone",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown area kind."),
    };

    private static AreaKind? ParseKind(string? name) => name switch
    {
        "restricted" => AreaKind.Restricted,
        "confinement" => AreaKind.Confinement,
        "zone" => AreaKind.Zone,
        _ => null,
    };

    private static string OverlayName(OverlayKind kind) => kind switch
    {
        OverlayKind.Lens => "lens",
        OverlayKind.ZoneOverlap => "zoneOverlap",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown overlay kind."),
    };

    private static CommandKind OverlayCommandKind(OverlayKind kind)
        => kind == OverlayKind.Lens ? CommandKind.Lens : CommandKind.ZoneOverlap;

    private void Send(object command) => Append(new SerializedCommand(command));

    /// <summary>Adds or replaces the batch's command for <paramref name="key"/>.</summary>
    private void Upsert(CommandKey key, MapCommand command)
    {
        if (batchIndex.TryGetValue(key, out int index))
        {
            batch[index] = command;
            return;
        }

        batchIndex.Add(key, batch.Count);
        Append(command);
    }

    /// <summary>Drops the batch's pending upsert for <paramref name="key"/>, if any, and appends the removal.</summary>
    private void Remove(CommandKey key, object command)
    {
        if (batchIndex.Remove(key, out int index))
            batch[index] = null;

        Send(command);
    }

    private void Append(MapCommand command)
    {
        batch.Add(command);

        // Flushed once the current dispatcher operation (the tick or the click handler) has
        // returned; Normal runs before the next tick, which the timer raises at Background.
        // Before the map is ready the batch just grows; upserts are merged, so it is bounded
        // by the number of vehicles, pairs and zones plus the user's own actions.
        if (!flushScheduled && ready)
        {
            flushScheduled = true;
            webView.Dispatcher.BeginInvoke(DispatcherPriority.Normal, flush);
        }
    }

    private void Flush()
    {
        flushScheduled = false;
        if (batch.Count == 0)
            return;

        long start = DemoMetrics.BeginSend();
        json.ResetWrittenCount();
        using (Utf8JsonWriter writer = new(json))
        {
            writer.WriteStartArray();
            foreach (MapCommand? command in batch)
                command?.Write(writer, outlines);

            writer.WriteEndArray();
        }

        batch.Clear();
        batchIndex.Clear();
        string message = Encoding.UTF8.GetString(json.WrittenSpan);
        webView.CoreWebView2.PostWebMessageAsJson(message);
        DemoMetrics.EndSend(start, message);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        JsonDocument message;
        try
        {
            message = JsonDocument.Parse(e.WebMessageAsJson);
        }
        catch (JsonException)
        {
            return; // an unparseable message must not bring the UI thread down
        }

        using (message)
        {
            HandleWebMessage(message);
        }
    }

    private void HandleWebMessage(JsonDocument message)
    {
        if (!message.RootElement.TryGetProperty("type", out JsonElement typeElement))
            return;

        switch (typeElement.GetString())
        {
            case "ready":
                ready = true;
                Flush();
                break;

            case "areaDrawn":
                JsonElement root = message.RootElement;
                if (ParseKind(root.GetProperty("kind").GetString()) is AreaKind kind)
                {
                    AreaDrawn?.Invoke(
                        kind,
                        new GeoPoint(root.GetProperty("south").GetDouble(), root.GetProperty("west").GetDouble()),
                        new GeoPoint(root.GetProperty("north").GetDouble(), root.GetProperty("east").GetDouble()));
                }

                break;

            case "drawCancelled":
                DrawCancelled?.Invoke();
                break;
        }
    }

    /// <summary>What a batch entry is about: commands with the same key replace one another.</summary>
    private readonly record struct CommandKey(CommandKind Kind, string Id);
}
