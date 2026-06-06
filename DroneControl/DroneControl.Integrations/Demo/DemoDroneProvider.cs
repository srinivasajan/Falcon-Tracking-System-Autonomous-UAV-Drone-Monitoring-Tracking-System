using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

namespace DroneControl.Integrations.Demo;

public class DemoDroneProvider : IDroneProvider
{
    public string ProviderName => "Demo Drone";

    public DroneConnectionState ConnectionState { get; private set; } = DroneConnectionState.Disconnected;
    public TelemetrySnapshot CurrentTelemetry { get; private set; }

    public event EventHandler<DroneConnectionState>? ConnectionStateChanged;
    public event EventHandler<TelemetrySnapshot>? TelemetryUpdated;

    private CancellationTokenSource? _telemetryCts;
    private double _lat = 47.3977;
    private double _lon = 8.5456;
    private double _alt = 0;
    private double _targetLat;
    private double _targetLon;
    private bool _isFlying;
    private string _mode = "Ready";

    public DemoDroneProvider()
    {
        CurrentTelemetry = new TelemetrySnapshot(DateTimeOffset.Now, false, 0, 0, 0, 0, 0, "Disconnected");
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ConnectionState = DroneConnectionState.Connected;
        ConnectionStateChanged?.Invoke(this, ConnectionState);
        
        _telemetryCts = new CancellationTokenSource();
        _ = RunTelemetryLoopAsync(_telemetryCts.Token);
        
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _telemetryCts?.Cancel();
        ConnectionState = DroneConnectionState.Disconnected;
        ConnectionStateChanged?.Invoke(this, ConnectionState);
        return Task.CompletedTask;
    }

    public Task ArmAsync(CancellationToken cancellationToken = default)
    {
        _mode = "Armed";
        return Task.CompletedTask;
    }

    public Task DisarmAsync(CancellationToken cancellationToken = default)
    {
        _mode = "Disarmed";
        _isFlying = false;
        return Task.CompletedTask;
    }

    public Task TakeoffAsync(CancellationToken cancellationToken = default)
    {
        _mode = "Takeoff";
        _alt = 10;
        _isFlying = true;
        return Task.CompletedTask;
    }

    public Task LandAsync(CancellationToken cancellationToken = default)
    {
        _mode = "Landing";
        _alt = 0;
        _isFlying = false;
        return Task.CompletedTask;
    }

    public Task GotoAsync(double latitude, double longitude, double altitudeMeters, CancellationToken cancellationToken = default)
    {
        _mode = "Pursuit";
        _targetLat = latitude;
        _targetLon = longitude;
        return Task.CompletedTask;
    }

    public Task ReturnToLaunchAsync(CancellationToken cancellationToken = default)
    {
        _mode = "RTL";
        _targetLat = 47.3977;
        _targetLon = 8.5456;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _telemetryCts?.Cancel();
        return ValueTask.CompletedTask;
    }

    private async Task RunTelemetryLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        while (await timer.WaitForNextTickAsync(token))
        {
            if (_isFlying && _mode == "Pursuit")
            {
                // Move 10% towards target each tick
                _lat += (_targetLat - _lat) * 0.1;
                _lon += (_targetLon - _lon) * 0.1;
            }

            CurrentTelemetry = new TelemetrySnapshot(
                DateTimeOffset.Now,
                IsConnected: true,
                BatteryPercent: 95,
                Latitude: _lat,
                Longitude: _lon,
                SpeedMetersPerSecond: _mode == "Pursuit" ? 5.5 : 0,
                AltitudeMeters: _alt,
                Mode: _mode
            );
            
            TelemetryUpdated?.Invoke(this, CurrentTelemetry);
        }
    }
}
