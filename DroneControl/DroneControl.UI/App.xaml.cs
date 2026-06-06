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

                // Step 1: register mock providers (simulator, mock vision, mock tracking, FFmpeg)
                services.AddDevelopmentMockProviders();

                // Step 2: upgrade tracker to IoU in ALL modes
                //         (IouTrackingProvider is last-registered → wins over mock tracker)
                services.AddIouTrackerOnly();

                // Step 3: register CommandPlanner so the UI can plan commands
                services.AddCommandPlanner();

                var providerMode = context.Configuration["DroneControl:Mode"] ?? "Mock";

                if (providerMode.Equals("Real", StringComparison.OrdinalIgnoreCase))
                {
                    // Real mode: use YOLO + real drone
                    services.AddVisionTrackingProviders();
                    services.AddPx4MavsdkProviders();
                }
                else if (providerMode.Equals("Simulation", StringComparison.OrdinalIgnoreCase))
                {
                    // Simulation mode: mock vision/simulator, real drone (MAVSDK/PX4 SITL)
                    // Vision stays as TemporaryMockVisionProvider (no camera HW needed)
                    services.AddPx4MavsdkProviders();
                }
                // Mock mode: everything stays as mocks

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

        try
        {
            _host.Services.GetRequiredService<StoragePaths>().EnsureCreated();
            await _host.StartAsync();

            MainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            File.WriteAllText("startup_error.txt", ex.ToString());
            MessageBox.Show(ex.ToString(), "Startup Error");
            Environment.Exit(1);
        }
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
