using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Abstractions;

public interface IManagedRuntime
{
    RuntimeDefinition Definition { get; }
    RuntimeHealthSnapshot CurrentHealth { get; }
    Task<RuntimeHealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default);
    Task InstallAsync(RuntimePackageManifest manifest, CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task RestartAsync(CancellationToken cancellationToken = default);
}
