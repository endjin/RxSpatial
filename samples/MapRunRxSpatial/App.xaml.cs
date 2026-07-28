using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using RxSpatial;

namespace MapRunRxSpatial
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            RxSpatialOptions.Default =
                configuration.GetSection(RxSpatialOptions.SectionName).Get<RxSpatialOptions>() ?? new RxSpatialOptions();

            base.OnStartup(e);
        }
    }
}
