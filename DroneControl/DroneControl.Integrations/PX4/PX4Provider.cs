using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Integrations.MAVSDK;
using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DroneControl.Integrations.PX4;

public sealed class PX4Provider : IDroneProvider
{
    private readonly MavsdkProvider _mavsdkProvider;
    private readonly IRuntimeRegistry _runtimeRegistry;
    private readonly IRuntimePathResolver _pathResolver;
    private readonly IRuntimeProcessManager _processManager;
    private readonly IRuntimeHealthMonitor _runtimeHealthMonitor;
    private readonly Px4ProviderOptions _options;
    private readonly ILogger<PX4Provider> _logger;

    public PX4Provider(
        MavsdkProvider mavsdkProvider,
        IRuntimeRegistry runtimeRegistry,
        IRuntimePathResolver pathResolver,
        IRuntimeProcessManager processManager,
        IRuntimeHealthMonitor runtimeHealthMonitor,
        IOptions<Px4ProviderOptions> options,
        ILogger<PX4Provider> logger)
    {
        _mavsdkProvider = mavsdkProvider;
        _runtimeRegistry = runtimeRegistry;
        _pathResolver = pathResolver;
        _processManager = processManager;
        _runtimeHealthMonitor = runtimeHealthMonitor;
        _options = options.Value;
        _logger = logger;

        _mavsdkProvider.ConnectionStateChanged += (_, state) => ConnectionStateChanged?.Invoke(this, state);
        _mavsdkProvider.TelemetryUpdated += (_, telemetry) => TelemetryUpdated?.Invoke(this, telemetry);
    }

    public string ProviderName => "PX4 via MAVSDK";
    public DroneConnectionState ConnectionState => _mavsdkProvider.ConnectionState;
    public TelemetrySnapshot CurrentTelemetry => _mavsdkProvider.CurrentTelemetry;

    public event EventHandler<DroneConnectionState>? ConnectionStateChanged;
    public event EventHandler<TelemetrySnapshot>? TelemetryUpdated;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting PX4 provider.");
        
        if (_options.ConnectionMode == ConnectionMode.ManagedLocal)
        {
            await StartPx4Async(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), cancellationToken);
        }
        else
        {
            _logger.LogInformation("ConnectionMode is ExternalEndpoint. Skipping local PX4 launch.");
        }

        await _mavsdkProvider.ConnectAsync(cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting PX4 provider.");
        await _mavsdkProvider.DisconnectAsync(cancellationToken);

        if (_options.ConnectionMode == ConnectionMode.ManagedLocal)
        {
            await _processManager.StopAsync(RuntimeId.Px4, TimeSpan.FromSeconds(10), cancellationToken);
            _runtimeHealthMonitor.Report(_runtimeHealthMonitor.GetLatest(RuntimeId.Px4) with
            {
                Status = RuntimeStatus.Stopped,
                UpdatedAt = DateTimeOffset.Now
            });
        }
    }

    public Task ArmAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PX4 arm requested.");
        return _mavsdkProvider.ArmAsync(cancellationToken);
    }

    public Task DisarmAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PX4 disarm requested.");
        return _mavsdkProvider.DisarmAsync(cancellationToken);
    }

    public Task TakeoffAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PX4 takeoff requested.");
        return _mavsdkProvider.TakeoffAsync(cancellationToken);
    }

    public Task LandAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PX4 land requested.");
        return _mavsdkProvider.LandAsync(cancellationToken);
    }

    public Task GotoAsync(double latitude, double longitude, double altitudeMeters, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PX4 goto requested.");
        return _mavsdkProvider.GotoAsync(latitude, longitude, altitudeMeters, cancellationToken);
    }

    public Task ReturnToLaunchAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PX4 RTL requested.");
        return _mavsdkProvider.ReturnToLaunchAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private async Task StartPx4Async(CancellationToken cancellationToken)
    {
        var definition = _runtimeRegistry.GetRequired(RuntimeId.Px4);
        var executablePath = _pathResolver.GetExecutablePath(definition);
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            ReportPx4Failure($"PX4 executable missing at '{executablePath}'.");
            throw new FileNotFoundException("PX4 executable was not found.", executablePath);
        }

        if (_processManager.IsRunning(RuntimeId.Px4))
        {
            return;
        }

        var installDirectory = _pathResolver.GetInstallDirectory(definition);
        var startupScript = Path.Combine(installDirectory, _options.StartupScriptRelativePath);
        if (!File.Exists(startupScript))
        {
            ReportPx4Failure($"PX4 startup script missing at '{startupScript}'.");
            throw new FileNotFoundException("PX4 startup script was not found.", startupScript);
        }

        _runtimeHealthMonitor.Report(_runtimeHealthMonitor.GetLatest(RuntimeId.Px4) with
        {
            Status = RuntimeStatus.Starting,
            UpdatedAt = DateTimeOffset.Now
        });

        var request = new RuntimeProcessStartRequest(
            RuntimeId.Px4,
            executablePath,
            ["-s", startupScript],
            installDirectory,
            new Dictionary<string, string>
            {
                ["PX4_SIM_MODEL"] = _options.Model,
                ["PX4_GZ_WORLD"] = _options.World
            });

        var handle = await _processManager.StartAsync(request, cancellationToken);
        _runtimeHealthMonitor.Report(_runtimeHealthMonitor.GetLatest(RuntimeId.Px4) with
        {
            Status = RuntimeStatus.Running,
            IsInstalled = true,
            ProcessId = handle.ProcessId,
            UpdatedAt = DateTimeOffset.Now
        });
    }

    private void ReportPx4Failure(string message)
    {
        var current = _runtimeHealthMonitor.GetLatest(RuntimeId.Px4);
        _runtimeHealthMonitor.Report(current with
        {
            Status = RuntimeStatus.Failed,
            UpdatedAt = DateTimeOffset.Now,
            Diagnostics = current.Diagnostics.Concat([
                new RuntimeDiagnostic(RuntimeDiagnosticSeverity.Error, "px4_start_failed", message, DateTimeOffset.Now)
            ]).ToArray()
        });
    }
}
