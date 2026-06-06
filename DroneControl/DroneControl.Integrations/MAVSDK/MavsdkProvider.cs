using DroneControl.Core.Models;
using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Models;
using Grpc.Core;
using Grpc.Net.Client;
using Mavsdk.Rpc.Action;
using Mavsdk.Rpc.Core;
using Mavsdk.Rpc.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DroneControl.Integrations.MAVSDK;

public sealed class MavsdkProvider : DroneControl.Core.Abstractions.IDroneProvider
{
    private readonly IRuntimePathResolver _pathResolver;
    private readonly IRuntimeRegistry _runtimeRegistry;
    private readonly IRuntimeProcessManager _processManager;
    private readonly IRuntimeHealthMonitor _runtimeHealthMonitor;
    private readonly MavsdkProviderOptions _options;
    private readonly ILogger<MavsdkProvider> _logger;
    private readonly object _sync = new();
    private CancellationTokenSource? _subscriptionsCts;
    private GrpcChannel? _channel;
    private TelemetryService.TelemetryServiceClient? _telemetryClient;
    private ActionService.ActionServiceClient? _actionClient;
    private CoreService.CoreServiceClient? _coreClient;
    private TelemetrySnapshot _telemetry = DisconnectedTelemetry("MAVSDK");

    public MavsdkProvider(
        IRuntimePathResolver pathResolver,
        IRuntimeRegistry runtimeRegistry,
        IRuntimeProcessManager processManager,
        IRuntimeHealthMonitor runtimeHealthMonitor,
        IOptions<MavsdkProviderOptions> options,
        ILogger<MavsdkProvider> logger)
    {
        _pathResolver = pathResolver;
        _runtimeRegistry = runtimeRegistry;
        _processManager = processManager;
        _runtimeHealthMonitor = runtimeHealthMonitor;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "MAVSDK";

    public event EventHandler<DroneConnectionState>? ConnectionStateChanged;
    public event EventHandler<TelemetrySnapshot>? TelemetryUpdated;

    public DroneConnectionState ConnectionState { get; private set; } = DroneConnectionState.Disconnected;
    public TelemetrySnapshot CurrentTelemetry => _telemetry;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        SetConnectionState(DroneConnectionState.Connecting);
        
        if (_options.ConnectionMode == ConnectionMode.ManagedLocal)
        {
            await StartMavsdkServerAsync(cancellationToken);
        }
        else
        {
            _logger.LogInformation("ConnectionMode is ExternalEndpoint. Skipping local MAVSDK launch. Connecting to external gRPC endpoint at {GrpcAddress}", _options.GrpcAddress);
        }

        _channel = GrpcChannel.ForAddress(_options.GrpcAddress);
        _telemetryClient = new TelemetryService.TelemetryServiceClient(_channel);
        _actionClient = new ActionService.ActionServiceClient(_channel);
        _coreClient = new CoreService.CoreServiceClient(_channel);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds));

        await WaitForVehicleConnectionAsync(timeoutCts.Token);
        StartTelemetrySubscriptions();
        SetConnectionState(DroneConnectionState.Connected);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting MAVSDK provider.");
        _subscriptionsCts?.Cancel();
        _subscriptionsCts?.Dispose();
        _subscriptionsCts = null;

        if (_channel is not null)
        {
            _channel.Dispose();
            _channel = null;
            await Task.CompletedTask;
        }

        _telemetryClient = null;
        _actionClient = null;
        _coreClient = null;

        if (_options.ConnectionMode == ConnectionMode.ManagedLocal)
        {
            await _processManager.StopAsync(RuntimeId.Mavsdk, TimeSpan.FromSeconds(10), cancellationToken);
        }

        SetConnectionState(DroneConnectionState.Disconnected);
        PublishTelemetry(_telemetry with { IsConnected = false, Mode = "Disconnected" });
    }

    public Task ArmAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteActionAsync("Arm", async client => (await client.ArmAsync(new ArmRequest(), cancellationToken: cancellationToken).ResponseAsync).ActionResult);
    }

    public Task DisarmAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteActionAsync("Disarm", async client => (await client.DisarmAsync(new DisarmRequest(), cancellationToken: cancellationToken).ResponseAsync).ActionResult);
    }

    public Task TakeoffAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteActionAsync("Takeoff", async client => (await client.TakeoffAsync(new TakeoffRequest(), cancellationToken: cancellationToken).ResponseAsync).ActionResult);
    }

    public Task LandAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteActionAsync("Land", async client => (await client.LandAsync(new LandRequest(), cancellationToken: cancellationToken).ResponseAsync).ActionResult);
    }

    public Task GotoAsync(double latitude, double longitude, double altitudeMeters, CancellationToken cancellationToken = default)
    {
        var req = new GotoLocationRequest
        {
            LatitudeDeg = latitude,
            LongitudeDeg = longitude,
            AbsoluteAltitudeM = (float)altitudeMeters,
            YawDeg = 0
        };
        return ExecuteActionAsync("GotoLocation", async client => (await client.GotoLocationAsync(req, cancellationToken: cancellationToken).ResponseAsync).ActionResult);
    }

    public Task ReturnToLaunchAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteActionAsync("ReturnToLaunch", async client => (await client.ReturnToLaunchAsync(new ReturnToLaunchRequest(), cancellationToken: cancellationToken).ResponseAsync).ActionResult);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    private async Task StartMavsdkServerAsync(CancellationToken cancellationToken)
    {
        var definition = _runtimeRegistry.GetRequired(RuntimeId.Mavsdk);
        var executablePath = _pathResolver.GetExecutablePath(definition);
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            ReportRuntimeFailure(RuntimeId.Mavsdk, $"MAVSDK server executable missing at '{executablePath}'.");
            throw new FileNotFoundException("MAVSDK server executable was not found.", executablePath);
        }

        if (_processManager.IsRunning(RuntimeId.Mavsdk))
        {
            return;
        }

        var request = new RuntimeProcessStartRequest(
            RuntimeId.Mavsdk,
            executablePath,
            ["-p", _options.GrpcPort.ToString(), _options.SystemAddress],
            _pathResolver.GetInstallDirectory(definition),
            new Dictionary<string, string>());

        _runtimeHealthMonitor.Report(_runtimeHealthMonitor.GetLatest(RuntimeId.Mavsdk) with
        {
            Status = RuntimeStatus.Starting,
            UpdatedAt = DateTimeOffset.Now
        });

        var handle = await _processManager.StartAsync(request, cancellationToken);
        _runtimeHealthMonitor.Report(_runtimeHealthMonitor.GetLatest(RuntimeId.Mavsdk) with
        {
            Status = RuntimeStatus.Running,
            IsInstalled = true,
            ProcessId = handle.ProcessId,
            UpdatedAt = DateTimeOffset.Now
        });
    }

    private async Task WaitForVehicleConnectionAsync(CancellationToken cancellationToken)
    {
        if (_coreClient is null)
        {
            throw new InvalidOperationException("MAVSDK core client is not initialized.");
        }

        _logger.LogInformation("Waiting for MAVSDK vehicle connection on {SystemAddress}.", _options.SystemAddress);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var call = _coreClient.SubscribeConnectionState(new SubscribeConnectionStateRequest(), cancellationToken: cancellationToken);
                await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken))
                {
                    if (response.ConnectionState.IsConnected)
                    {
                        _logger.LogInformation("MAVSDK vehicle connection established.");
                        return;
                    }
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("MAVSDK gRPC endpoint is not ready yet.");
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        throw new OperationCanceledException("MAVSDK vehicle connection timed out.", cancellationToken);
    }

    private void StartTelemetrySubscriptions()
    {
        if (_telemetryClient is null)
        {
            throw new InvalidOperationException("MAVSDK telemetry client is not initialized.");
        }

        _subscriptionsCts?.Cancel();
        _subscriptionsCts = new CancellationTokenSource();
        var token = _subscriptionsCts.Token;

        _ = RunSubscriptionAsync("position", token, async ct =>
        {
            using var call = _telemetryClient.SubscribePosition(new SubscribePositionRequest(), cancellationToken: ct);
            await foreach (var response in call.ResponseStream.ReadAllAsync(ct))
            {
                PublishTelemetry(_telemetry with
                {
                    IsConnected = true,
                    Latitude = response.Position.LatitudeDeg,
                    Longitude = response.Position.LongitudeDeg,
                    AltitudeMeters = response.Position.RelativeAltitudeM
                });
            }
        });

        _ = RunSubscriptionAsync("battery", token, async ct =>
        {
            using var call = _telemetryClient.SubscribeBattery(new SubscribeBatteryRequest(), cancellationToken: ct);
            await foreach (var response in call.ResponseStream.ReadAllAsync(ct))
            {
                PublishTelemetry(_telemetry with
                {
                    IsConnected = true,
                    BatteryPercent = NormalizeBattery(response.Battery.RemainingPercent)
                });
            }
        });

        _ = RunSubscriptionAsync("velocity", token, async ct =>
        {
            using var call = _telemetryClient.SubscribeVelocityNed(new SubscribeVelocityNedRequest(), cancellationToken: ct);
            await foreach (var response in call.ResponseStream.ReadAllAsync(ct))
            {
                var velocity = response.VelocityNed;
                PublishTelemetry(_telemetry with
                {
                    IsConnected = true,
                    SpeedMetersPerSecond = Math.Sqrt(
                        velocity.NorthMS * velocity.NorthMS +
                        velocity.EastMS * velocity.EastMS +
                        velocity.DownMS * velocity.DownMS)
                });
            }
        });

        _ = RunSubscriptionAsync("flight mode", token, async ct =>
        {
            using var call = _telemetryClient.SubscribeFlightMode(new SubscribeFlightModeRequest(), cancellationToken: ct);
            await foreach (var response in call.ResponseStream.ReadAllAsync(ct))
            {
                PublishTelemetry(_telemetry with
                {
                    IsConnected = true,
                    Mode = response.FlightMode.ToString().Replace("FlightMode", "", StringComparison.OrdinalIgnoreCase).Trim('_')
                });
            }
        });
    }

    private async Task RunSubscriptionAsync(string name, CancellationToken cancellationToken, Func<CancellationToken, Task> subscribe)
    {
        try
        {
            await subscribe(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MAVSDK telemetry subscription {SubscriptionName} failed.", name);
            SetConnectionState(DroneConnectionState.Faulted);
        }
    }

    private async Task ExecuteActionAsync(string actionName, Func<ActionService.ActionServiceClient, Task<ActionResult>> execute)
    {
        if (_actionClient is null)
        {
            throw new InvalidOperationException("MAVSDK action client is not connected.");
        }

        _logger.LogInformation("Sending MAVSDK action {ActionName}.", actionName);
        var result = await execute(_actionClient);
        if (Convert.ToInt32(result.Result) != 1)
        {
            _logger.LogWarning("MAVSDK action {ActionName} failed: {Result} {ResultText}.", actionName, result.Result, result.ResultStr);
            throw new Exception($"Action {actionName} failed: {result.ResultStr}");
        }
        else
        {
            _logger.LogInformation("MAVSDK action {ActionName} succeeded. ActionResult: {Result} ({ResultText})", actionName, result.Result, result.ResultStr);
        }
    }

    private void PublishTelemetry(TelemetrySnapshot telemetry)
    {
        lock (_sync)
        {
            _telemetry = telemetry with { Timestamp = DateTimeOffset.Now };
        }

        TelemetryUpdated?.Invoke(this, _telemetry);
    }

    private void SetConnectionState(DroneConnectionState state)
    {
        ConnectionState = state;
        ConnectionStateChanged?.Invoke(this, state);
    }

    private void ReportRuntimeFailure(RuntimeId runtimeId, string message)
    {
        var current = _runtimeHealthMonitor.GetLatest(runtimeId);
        _runtimeHealthMonitor.Report(current with
        {
            Status = RuntimeStatus.Failed,
            UpdatedAt = DateTimeOffset.Now,
            Diagnostics = current.Diagnostics.Concat([
                new RuntimeDiagnostic(RuntimeDiagnosticSeverity.Error, "runtime_start_failed", message, DateTimeOffset.Now)
            ]).ToArray()
        });
    }

    private static int NormalizeBattery(float remainingPercent)
    {
        if (float.IsNaN(remainingPercent))
        {
            return 0;
        }

        var value = remainingPercent <= 1 ? remainingPercent * 100 : remainingPercent;
        return (int)Math.Clamp(Math.Round(value), 0, 100);
    }

    private static TelemetrySnapshot DisconnectedTelemetry(string mode)
    {
        return new TelemetrySnapshot(
            DateTimeOffset.Now,
            IsConnected: false,
            BatteryPercent: 0,
            Latitude: 0,
            Longitude: 0,
            SpeedMetersPerSecond: 0,
            AltitudeMeters: 0,
            Mode: mode);
    }
}
