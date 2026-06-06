using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Abstractions;

public interface IRuntimeVersionStore
{
    Task<RuntimeVersionInfo> GetVersionAsync(RuntimeDefinition definition, CancellationToken cancellationToken = default);
    Task SetInstalledVersionAsync(RuntimeId runtimeId, string version, CancellationToken cancellationToken = default);
}
