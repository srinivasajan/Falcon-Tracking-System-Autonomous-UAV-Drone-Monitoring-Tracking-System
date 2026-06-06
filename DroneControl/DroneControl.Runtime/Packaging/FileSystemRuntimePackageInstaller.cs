using System.IO.Compression;
using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace DroneControl.Runtime.Packaging;

public sealed class FileSystemRuntimePackageInstaller : IRuntimePackageInstaller
{
    private readonly IRuntimeRegistry _runtimeRegistry;
    private readonly IRuntimePathResolver _pathResolver;
    private readonly IRuntimeVersionStore _versionStore;
    private readonly ILogger<FileSystemRuntimePackageInstaller> _logger;

    public FileSystemRuntimePackageInstaller(
        IRuntimeRegistry runtimeRegistry,
        IRuntimePathResolver pathResolver,
        IRuntimeVersionStore versionStore,
        ILogger<FileSystemRuntimePackageInstaller> logger)
    {
        _runtimeRegistry = runtimeRegistry;
        _pathResolver = pathResolver;
        _versionStore = versionStore;
        _logger = logger;
    }

    public async Task InstallAsync(RuntimePackageManifest manifest, CancellationToken cancellationToken = default)
    {
        var definition = _runtimeRegistry.GetRequired(manifest.RuntimeId);
        var installDirectory = _pathResolver.GetInstallDirectory(definition);
        Directory.CreateDirectory(installDirectory);

        _logger.LogInformation("Installing runtime {RuntimeId} from {SourcePath} to {InstallDirectory}.", manifest.RuntimeId, manifest.SourcePath, installDirectory);

        var sourcePath = manifest.SourcePath;
        if (sourcePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || sourcePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var tempFile = Path.GetTempFileName() + ".zip";
            _logger.LogInformation("Downloading {RuntimeId} from {Url} to {TempFile}", manifest.RuntimeId, sourcePath, tempFile);
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(sourcePath, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs, cancellationToken);
            fs.Close();
            sourcePath = tempFile;
        }

        if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(sourcePath, installDirectory, overwriteFiles: true);
            var dirs = Directory.GetDirectories(installDirectory);
            if (dirs.Length == 1 && Directory.GetFiles(installDirectory).Length == 0)
            {
                var rootDir = dirs[0];
                foreach (var f in Directory.GetFiles(rootDir, "*", SearchOption.AllDirectories))
                {
                    var target = f.Replace(rootDir, installDirectory, StringComparison.OrdinalIgnoreCase);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Move(f, target, overwrite: true);
                }
                Directory.Delete(rootDir, recursive: true);
            }
        }
        else if (Directory.Exists(sourcePath))
        {
            CopyDirectory(sourcePath, installDirectory, cancellationToken);
        }
        else
        {
            throw new FileNotFoundException($"Runtime package source '{sourcePath}' was not found.", sourcePath);
        }

        await _versionStore.SetInstalledVersionAsync(manifest.RuntimeId, manifest.Version, cancellationToken);
    }

    public Task<bool> IsInstalledAsync(RuntimeDefinition definition, CancellationToken cancellationToken = default)
    {
        var installDirectory = _pathResolver.GetInstallDirectory(definition);
        var executablePath = _pathResolver.GetExecutablePath(definition);
        var installed = Directory.Exists(installDirectory)
            && (executablePath is null || File.Exists(executablePath));

        return Task.FromResult(installed);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory, CancellationToken cancellationToken)
    {
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = directory.Replace(sourceDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = file.Replace(sourceDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
