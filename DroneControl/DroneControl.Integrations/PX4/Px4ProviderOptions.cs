using DroneControl.Core.Models;

namespace DroneControl.Integrations.PX4;

public sealed class Px4ProviderOptions
{
    public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.ManagedLocal;
    public string StartupScriptRelativePath { get; set; } = "etc\\init.d-posix\\rcS";
    public string Model { get; set; } = "x500";
    public string World { get; set; } = "default";
    public int StartupDelaySeconds { get; set; } = 3;
}
