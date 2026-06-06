using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Abstractions;

public interface IRuntimePathResolver
{
    string RuntimeRoot { get; }
    string GetInstallDirectory(RuntimeDefinition definition);
    string? GetExecutablePath(RuntimeDefinition definition);
}
