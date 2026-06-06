namespace DroneControl.Runtime.Models;

public sealed record RuntimeVersionInfo(
    string? InstalledVersion,
    string SupportedVersion,
    bool IsUpgradeAvailable,
    DateTimeOffset? CheckedAt);
