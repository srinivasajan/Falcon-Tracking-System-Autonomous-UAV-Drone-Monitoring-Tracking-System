using System.IO;
using System.Windows;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Services;
using DroneControl.Integrations;
using DroneControl.Runtime;
using DroneControl.Storage;
using DroneControl.UI.ViewModels;
using DroneControl.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Unosquare.FFME;

namespace DroneControl.UI;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            Library.FFmpegDirectory = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "bin");
        }
        catch { }

        _host = Host.CreateDefaultBuilder(e.Args)
            .UseSerilog((context, services, configuration) =>
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DroneControl",
                    "logs",
                    "dronecontrol-.log");

                configuration
                    .MinimumLevel.Information()
                    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddEventLog();
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<RuntimeManagementOptions>(
                    context.Configuration.GetSection("DroneControl:RuntimeManagement"));
                services.Configure<DroneControl.Integrations.PX4.Px4ProviderOptions>(
                    context.Configuration.GetSection("DroneControl:PX4"));
                services.Configure<DroneControl.Integrations.MAVSDK.MavsdkProviderOptions>(
                    context.Configuration.GetSection("DroneControl:MAVSDK"));
                services.AddSingleton<StoragePaths>();
                services.AddDroneControlRuntimeManagement();
                services.AddDroneControlStorage();
                services.AddSingleton<ITargetLockService, TargetLockService>();

                // All providers start as mocks. In Simulation/Real mode, IDroneProvider
                // is overridden with PX4Provider (last-registration wins in MS DI).
                services.AddDevelopmentMockProviders();
                
                // Override Vision and Tracking with real implementations
                services.AddVisionTrackingProviders();

                var providerMode = context.Configuration["DroneControl:Mode"] ?? "Mock";
                if (providerMode.Equals("Simulation", StringComparison.OrdinalIgnoreCase) ||
                    providerMode.Equals("Real", StringComparison.OrdinalIgnoreCase))
                {
                    services.AddPx4MavsdkProviders();
                }

                services.AddTransient<ReplayViewModel>(provider =>
                    new ReplayViewModel(
                        provider.GetRequiredService<ITelemetryRecorder>(),
                        provider.GetRequiredService<IVideoProvider>(),
                        provider.GetRequiredService<IDetectionRepository>(),
                        provider.GetRequiredService<ITrackingRepository>(),
                        provider.GetRequiredService<ITargetLockRepository>()
                    ));
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Services.GetRequiredService<StoragePaths>().EnsureCreated();
        await _host.StartAsync();

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
