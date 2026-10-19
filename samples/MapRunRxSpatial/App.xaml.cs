using System.Windows;
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

        // Before the main window (StartupUri) is created, so it never draws the failing colours.
        AccessibleTheme.Apply(this);

        base.OnStartup(e);
    }
}
