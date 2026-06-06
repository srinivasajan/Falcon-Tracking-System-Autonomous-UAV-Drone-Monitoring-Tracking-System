using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface IDroneProvider : IAsyncDisposable
{
    string ProviderName { get; }
    DroneConnectionState ConnectionState { get; }
    TelemetrySnapshot CurrentTelemetry { get; }
    event EventHandler<DroneConnectionState>? ConnectionStateChanged;
    event EventHandler<TelemetrySnapshot>? TelemetryUpdated;
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task ArmAsync(CancellationToken cancellationToken = default);
    Task DisarmAsync(CancellationToken cancellationToken = default);
    Task TakeoffAsync(CancellationToken cancellationToken = default);
    Task LandAsync(CancellationToken cancellationToken = default);
    Task GotoAsync(double latitude, double longitude, double altitudeMeters, CancellationToken cancellationToken = default);
    Task ReturnToLaunchAsync(CancellationToken cancellationToken = default);
}
