namespace DroneControl.Core.Models;

public sealed record ExternalComponent(
    string Name,
    string Purpose,
    Uri ProjectUri,
    string BundlingStrategy);
