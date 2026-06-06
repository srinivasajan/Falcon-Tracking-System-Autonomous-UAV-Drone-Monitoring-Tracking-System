using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface ISimulatorProvider : IAsyncDisposable
{
    string ProviderName { get; }
    SimulatorState State { get; }
    event EventHandler<SimulatorState>? StateChanged;
    event EventHandler<CameraFrame>? VirtualCameraFrameReady;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
