using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Integrations.Mocks;

/// <summary>
/// Temporary development mock only. Production drone communication should use MAVSDK/MAVLink providers.
/// </summary>
public sealed class TemporaryMockDroneProvider : IDroneProvider
{
    private readonly ILogger<TemporaryMockDroneProvider> _logger;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private int _tick;

    public TemporaryMockDroneProvider(ILogger<TemporaryMockDroneProvider> logger)
    {
        _logger = logger;
        CurrentTelemetry = CreateTelemetry(0, DroneConnectionState.Disconnected);
    }

    public string ProviderName => "Temporary Mock Drone";
    public DroneConnectionState ConnectionState { get; private set; } = DroneConnectionState.Disconnected;
    public TelemetrySnapshot CurrentTelemetry { get; private set; }

    public event EventHandler<DroneConnectionState>? ConnectionStateChanged;
    public event EventHandler<TelemetrySnapshot>? TelemetryUpdated;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_runTask is { IsCompleted: false })
        {
            return Task.CompletedTask;
        }

        SetConnectionState(DroneConnectionState.Connecting);
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = RunAsync(_runCts.Token);
        SetConnectionState(DroneConnectionState.Connected);
        return Task.CompletedTask;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _runCts?.Cancel();
        if (_runTask is not null)
        {
            await _runTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        SetConnectionState(DroneConnectionState.Disconnected);
    }

    public Task ArmAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Development mock arm command accepted.");
        return Task.CompletedTask;
    }

    public Task DisarmAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Development mock disarm command accepted.");
        return Task.CompletedTask;
    }

    public Task TakeoffAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Development mock takeoff command accepted.");
        return Task.CompletedTask;
    }

    public async Task LandAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock Drone landing.");
        await Task.Delay(1000, cancellationToken);
    }

    public Task GotoAsync(double latitude, double longitude, double altitudeMeters, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock Drone goto location.");
        return Task.CompletedTask;
    }

    public Task ReturnToLaunchAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock Drone RTL.");
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _runCts?.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                _tick++;
                CurrentTelemetry = CreateTelemetry(_tick, ConnectionState);
                TelemetryUpdated?.Invoke(this, CurrentTelemetry);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Temporary mock drone loop stopped.");
        }
    }

    private void SetConnectionState(DroneConnectionState state)
    {
        ConnectionState = state;
        ConnectionStateChanged?.Invoke(this, state);
    }

    private static TelemetrySnapshot CreateTelemetry(int tick, DroneConnectionState state)
    {
        var wave = Math.Sin(tick / 8d);
        return new TelemetrySnapshot(
            DateTimeOffset.Now,
            IsConnected: state == DroneConnectionState.Connected,
            BatteryPercent: Math.Max(15, 100 - tick / 12),
            Latitude: 37.7749 + tick * 0.00002,
            Longitude: -122.4194 + wave * 0.0005,
            SpeedMetersPerSecond: 6.5 + Math.Abs(wave) * 3,
            AltitudeMeters: 45 + Math.Sin(tick / 5d) * 8,
            Mode: "Development Mock");
    }
}
