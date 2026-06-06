using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Models;
using Microsoft.Extensions.Options;

namespace DroneControl.Runtime.Registry;

public sealed class RuntimePathResolver : IRuntimePathResolver
{
    public RuntimePathResolver(IOptions<RuntimeManagementOptions> options)
    {
        RuntimeRoot = ExpandPath(options.Value.RuntimeRoot)
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DroneControl",
                "runtimes");
    }

    public string RuntimeRoot { get; }

    public string GetInstallDirectory(RuntimeDefinition definition)
    {
        return Path.Combine(RuntimeRoot, definition.RelativeInstallPath);
    }

    public string? GetExecutablePath(RuntimeDefinition definition)
    {
        return definition.ExecutableRelativePath is null
            ? null
            : Path.Combine(GetInstallDirectory(definition), definition.ExecutableRelativePath);
    }

    private static string? ExpandPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Environment.ExpandEnvironmentVariables(path);
    }
}
