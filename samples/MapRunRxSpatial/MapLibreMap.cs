using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using RxSpatial.Geo;

namespace MapRunRxSpatial;

/// <summary>
/// A thin C# facade over MapLibre GL JS running in a WebView2 (see wwwroot/map.html).
/// Commands are posted as JSON web messages; map events (ready, areaDrawn, drawCancelled)
/// come back the same way. Commands sent before the map finishes loading are queued.
/// All members must be used from the UI thread (a WebView2 requirement).
/// </summary>
internal sealed class MapLibreMap(WebView2 webView)
{
    /// <summary>
    /// Vertices of a drawn reach circle: as many as map.html drew itself before, which keeps
    /// the twenty markers' messages small at 25 updates a second. The chord sag at the 3 km
    /// maximum reach is under 4 m, finer than a pixel at the Overlap tab's zoom.
    /// </summary>
    private const int ReachSegments = 64;

    private readonly List<string> pending = [];
    private bool ready;

    /// <summary>
    /// Raised with the kind and two opposite corners (south-west, north-east) when the user
    /// finishes dragging a rectangle in drawing mode. The map has already left drawing mode.
    /// </summary>
    public event Action<AreaKind, GeoPoint, GeoPoint>? AreaDrawn;

    /// <summary>Raised when the user cancels drawing mode in the map (Esc or a right-click).</summary>
    public event Action? DrawCancelled;

    public async Task InitializeAsync()
    {
        await webView.EnsureCoreWebView2Async();
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "maprun.local",
            Path.Combine(AppContext.BaseDirectory, "wwwroot"),
            CoreWebView2HostResourceAccessKind.Deny);
        webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        webView.CoreWebView2.Navigate("https://maprun.local/map.html");
    }

    /// <summary>
    /// Places (or moves) a vehicle marker. A non-null <paramref name="reach"/> also draws the
    /// vehicle's reach circle (the Overlap tab); null removes it. The circle's outline comes
    /// from <see cref="GeoOperations.ToVertices(GeoShape, int)"/>, so it is exactly the disc the queries use.
    /// </summary>
    public void SetMarker(string id, string label, GeoPoint position, MarkerStyle style, GeoCircle? reach)
        => Send(new
        {
            cmd = "setMarker", id, label, lat = position.Latitude, lon = position.Longitude,
            fill = style.Fill, border = style.Border, text = style.Text, shape = style.Shape,
            reach = reach is null ? null : Ring(GeoOperations.ToVertices(reach, ReachSegments)),
        });

    public void SetMarkerColor(string id, MarkerStyle style)
        => Send(new { cmd = "setMarkerColor", id, fill = style.Fill, border = style.Border, text = style.Text, shape = style.Shape });

    public void RemoveMarker(string id)
        => Send(new { cmd = "removeMarker", id });

    public void SetLine(string id, IEnumerable<GeoPoint> points, string color, double width, bool dashed, double opacity)
        => Send(new { cmd = "setLine", id, points = ToCoordinates(points), color, width, dashed, opacity });

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
        => Send(new { cmd = "removeShape", id });

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
    /// Draws (or redraws) a query-result region styled by its kind (OVERLAY_STYLES in map.html):
    /// <paramref name="polygons"/> are GeoJSON MultiPolygon coordinates. A non-null
    /// <paramref name="label"/> is shown in a pill beside the region, clear of the markers.
    /// </summary>
    public void SetOverlay(OverlayKind kind, string id, List<double[][][]> polygons, string? label)
        => Send(new { cmd = "setOverlay", kind = OverlayName(kind), id, polygons, label });

    public void RemoveOverlay(OverlayKind kind, string id)
        => Send(new { cmd = "removeOverlay", kind = OverlayName(kind), id });

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

    private static List<double[]> ToCoordinates(IEnumerable<GeoPoint> points)
    {
        // GeoJSON uses [longitude, latitude] ordering.
        List<double[]> coordinates = [];
        foreach (GeoPoint point in points)
            coordinates.Add([point.Longitude, point.Latitude]);

        return coordinates;
    }

    /// <summary>
    /// A closed GeoJSON linear ring ([longitude, latitude], first point repeated last) through
    /// <paramref name="vertices"/>, rounded to about 0.1 m to keep the web messages small.
    /// </summary>
    public static double[][] Ring(IReadOnlyList<GeoPoint> vertices)
    {
        double[][] ring = new double[vertices.Count + 1][];
        for (int i = 0; i < vertices.Count; i++)
            ring[i] = [Math.Round(vertices[i].Longitude, 6), Math.Round(vertices[i].Latitude, 6)];

        ring[^1] = ring[0];
        return ring;
    }

    private void Send(object command)
    {
        string json = JsonSerializer.Serialize(command);
        if (!ready)
        {
            // If the map never becomes ready (no network for the MapLibre CDN or the
            // tile server), the queue must not grow without bound under the once-a-second
            // vehicle updates; later messages are mostly upserts, so drop the oldest.
            if (pending.Count >= 4096)
                pending.RemoveAt(0);

            pending.Add(json);
            return;
        }

        Post(json);
    }

    private void Post(string json)
    {
        DemoMetrics.WebMessage(json);
        webView.CoreWebView2.PostWebMessageAsJson(json);
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
                foreach (string json in pending)
                    Post(json);

                pending.Clear();
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
}
