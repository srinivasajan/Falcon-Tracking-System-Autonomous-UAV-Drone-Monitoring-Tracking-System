using System.Text.Json;
using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Registry;

public sealed class FileRuntimeVersionStore : IRuntimeVersionStore
{
    private readonly IRuntimePathResolver _pathResolver;
    private readonly IRuntimeRegistry _runtimeRegistry;

    public FileRuntimeVersionStore(IRuntimePathResolver pathResolver, IRuntimeRegistry runtimeRegistry)
    {
        _pathResolver = pathResolver;
        _runtimeRegistry = runtimeRegistry;
    }

    public async Task<RuntimeVersionInfo> GetVersionAsync(RuntimeDefinition definition, CancellationToken cancellationToken = default)
    {
        var manifestPath = GetVersionManifestPath(definition);
        if (!File.Exists(manifestPath))
        {
            return new RuntimeVersionInfo(null, definition.SupportedVersion, false, DateTimeOffset.Now);
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<InstalledRuntimeManifest>(stream, cancellationToken: cancellationToken);
        var installedVersion = manifest?.Version;

        return new RuntimeVersionInfo(
            installedVersion,
            definition.SupportedVersion,
            IsUpgradeAvailable: !string.IsNullOrWhiteSpace(installedVersion)
                && definition.SupportedVersion != "TBD"
                && !StringComparer.OrdinalIgnoreCase.Equals(installedVersion, definition.SupportedVersion),
            CheckedAt: DateTimeOffset.Now);
    }

    public async Task SetInstalledVersionAsync(RuntimeId runtimeId, string version, CancellationToken cancellationToken = default)
    {
        var definition = _runtimeRegistry.GetRequired(runtimeId);
        var installDirectory = _pathResolver.GetInstallDirectory(definition);
        Directory.CreateDirectory(installDirectory);

        var manifest = new InstalledRuntimeManifest(runtimeId.Value, version, DateTimeOffset.Now);
        await using var stream = File.Create(GetVersionManifestPath(definition));
        await JsonSerializer.SerializeAsync(stream, manifest, cancellationToken: cancellationToken);
    }

    private string GetVersionManifestPath(RuntimeDefinition definition)
    {
        return Path.Combine(_pathResolver.GetInstallDirectory(definition), "dronecontrol-runtime.json");
    }

    private sealed record InstalledRuntimeManifest(string RuntimeId, string Version, DateTimeOffset InstalledAt);
}
