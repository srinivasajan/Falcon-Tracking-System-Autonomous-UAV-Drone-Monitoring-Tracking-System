namespace DroneControl.Plugins;

public interface IPluginManifest
{
    string Id { get; }
    string DisplayName { get; }
    Version Version { get; }
    IReadOnlyList<string> ProvidedCapabilities { get; }
}
