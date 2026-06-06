using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Abstractions;

public interface IRuntimePackageInstaller
{
    Task InstallAsync(RuntimePackageManifest manifest, CancellationToken cancellationToken = default);
    Task<bool> IsInstalledAsync(RuntimeDefinition definition, CancellationToken cancellationToken = default);
}
