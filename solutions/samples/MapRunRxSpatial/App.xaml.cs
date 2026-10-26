// Copyright (c) The RxSpatial Authors and Contributors. All rights reserved.
// Licensed under the MIT License.
// See the LICENSE file in the project root for more information.
// Modified by Endjin Limited.

using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.Configuration;
using RxSpatial;

namespace MapRunRxSpatial;

/// <summary>Loads RxSpatial options from appsettings.json before any window opens.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            RxSpatialOptions.Default =
                configuration.GetSection(RxSpatialOptions.SectionName).Get<RxSpatialOptions>() ?? new RxSpatialOptions();
        }
        catch (Exception ex)
        {
            // A hand-edited, malformed appsettings.json should not kill the demo silently.
            MessageBox.Show(
                "appsettings.json could not be read; continuing with default settings.\n\n" + ex.Message,
                "MapRunRxSpatial", MessageBoxButton.OK, MessageBoxImage.Warning);
            RxSpatialOptions.Default = new RxSpatialOptions();
        }

        // The demo requires visualized (synchronous) mode whatever the settings say: the
        // WebView2 map facade must be driven from the UI thread, and buffered mode notifies
        // zone observers from the thread pool.
        RxSpatialOptions.Default.IsVisualized = true;

        // WPF draws only the control panel: the map is a WebView2 child window, rendered by its
        // own GPU process. Hardware rendering gave WPF a Direct3D 9 device and swap chain for a
        // window sized to the work area: ~70 MB of private memory (driver heaps and mapped GPU
        // buffers), 65-90 MB of shared GPU memory and two dozen driver threads, for a panel
        // that redraws a few small rectangles a second. Software rendering draws the same panel
        // (only glyph and corner anti-aliasing differs, by at most a few levels) at no measurable
        // CPU cost. Set before the first window exists, so no device is created.
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        // Before the main window (StartupUri) is created, so it never draws the failing colours.
        AccessibleTheme.Apply(this);

        base.OnStartup(e);
    }
}
