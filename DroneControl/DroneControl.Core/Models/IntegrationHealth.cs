namespace DroneControl.Core.Models;

public sealed record IntegrationHealth(
    bool IsAvailable,
    string DisplayName,
    string Version,
    string Message);
