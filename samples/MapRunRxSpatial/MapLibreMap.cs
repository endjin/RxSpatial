using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MapRunRxSpatial
{
    /// <summary>
    /// A thin C# facade over MapLibre GL JS running in a WebView2 (see wwwroot/map.html).
    /// Commands are posted as JSON web messages; map events (ready, click, mousemove) come
    /// back the same way. Commands sent before the map finishes loading are queued.
    /// All members must be used from the UI thread (a WebView2 requirement).
    /// </summary>
    class MapLibreMap
    {
        private readonly WebView2 webView;
        private readonly List<string> pending = new List<string>();
        private bool ready;

        public MapLibreMap(WebView2 webView)
        {
            this.webView = webView;
        }

        /// <summary>Raised with (latitude, longitude) when the map is clicked.</summary>
        public event Action<double, double> MapClick;

        /// <summary>Raised with (latitude, longitude) as the pointer moves over the map (throttled).</summary>
        public event Action<double, double> MapMouseMove;

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

        public void SetMarker(string id, string label, double latitude, double longitude, string color)
            => Send(new { cmd = "setMarker", id, label, lat = latitude, lon = longitude, color });

        public void SetMarkerColor(string id, string color)
            => Send(new { cmd = "setMarkerColor", id, color });

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
            List<double[]> coordinates = new List<double[]>();
            foreach (GeoPoint point in points)
                coordinates.Add(new[] { point.Longitude, point.Latitude });
            return coordinates;
        }

        private void Send(object command)
        {
            string json = JsonSerializer.Serialize(command);
            if (!ready)
            {
                pending.Add(json);
                return;
            }

            webView.CoreWebView2.PostWebMessageAsJson(json);
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            using JsonDocument message = JsonDocument.Parse(e.WebMessageAsJson);
            string type = message.RootElement.GetProperty("type").GetString();
            switch (type)
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
}
