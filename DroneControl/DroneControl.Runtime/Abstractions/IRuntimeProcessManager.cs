using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Abstractions;

public interface IRuntimeProcessManager
{
    event EventHandler<RuntimeOutput>? OutputReceived;
    event EventHandler<RuntimeExit>? ProcessExited;
    bool IsRunning(RuntimeId runtimeId);
    Task<RuntimeProcessHandle> StartAsync(RuntimeProcessStartRequest request, CancellationToken cancellationToken = default);
    Task StopAsync(RuntimeId runtimeId, TimeSpan gracefulTimeout, CancellationToken cancellationToken = default);
    Task RestartAsync(RuntimeProcessStartRequest request, TimeSpan gracefulTimeout, CancellationToken cancellationToken = default);
}
