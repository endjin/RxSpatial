using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MapRunRxSpatial;

/// <summary>
/// A thin C# facade over MapLibre GL JS running in a WebView2 (see wwwroot/map.html).
/// Commands are posted as JSON web messages; map events (ready, click, mousemove) come
/// back the same way. Commands sent before the map finishes loading are queued.
/// All members must be used from the UI thread (a WebView2 requirement).
/// </summary>
internal sealed class MapLibreMap(WebView2 webView)
{
    private readonly List<string> pending = [];
    private bool ready;

    /// <summary>Raised with (latitude, longitude) when the map is clicked.</summary>
    public event Action<double, double>? MapClick;

    /// <summary>Raised with (latitude, longitude) as the pointer moves over the map (throttled).</summary>
    public event Action<double, double>? MapMouseMove;

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

    public void SetMarker(string id, string label, double latitude, double longitude, MarkerStyle style)
        => Send(new
        {
            cmd = "setMarker", id, label, lat = latitude, lon = longitude,
            fill = style.Fill, border = style.Border, text = style.Text,
        });

    public void SetMarkerColor(string id, MarkerStyle style)
        => Send(new { cmd = "setMarkerColor", id, fill = style.Fill, border = style.Border, text = style.Text });

    public void RemoveMarker(string id)
        => Send(new { cmd = "removeMarker", id });

    public void SetLine(string id, IEnumerable<GeoPoint> points, string color, double width, bool dashed, double opacity)
        => Send(new { cmd = "setLine", id, points = ToCoordinates(points), color, width, dashed, opacity });

    public void SetPolygon(
        string id, IEnumerable<GeoPoint> ring, string stroke, double strokeWidth, double strokeOpacity,
        string fill, double fillOpacity)
        => Send(new { cmd = "setPolygon", id, ring = ToCoordinates(ring), stroke, strokeWidth, strokeOpacity, fill, fillOpacity });

    public void RemoveShape(string id)
        => Send(new { cmd = "removeShape", id });

    private static List<double[]> ToCoordinates(IEnumerable<GeoPoint> points)
    {
        // GeoJSON uses [longitude, latitude] ordering.
        List<double[]> coordinates = [];
        foreach (GeoPoint point in points)
            coordinates.Add([point.Longitude, point.Latitude]);

        return coordinates;
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
                    webView.CoreWebView2.PostWebMessageAsJson(json);

                pending.Clear();
                break;

            case "click":
                MapClick?.Invoke(
                    message.RootElement.GetProperty("lat").GetDouble(),
                    message.RootElement.GetProperty("lon").GetDouble());
                break;

            case "mousemove":
                MapMouseMove?.Invoke(
                    message.RootElement.GetProperty("lat").GetDouble(),
                    message.RootElement.GetProperty("lon").GetDouble());
                break;
        }
    }
}
