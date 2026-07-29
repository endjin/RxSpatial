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

        base.OnStartup(e);
    }
}
