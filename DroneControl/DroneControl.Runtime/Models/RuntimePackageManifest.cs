namespace DroneControl.Runtime.Models;

public sealed record RuntimePackageManifest(
    RuntimeId RuntimeId,
    string Version,
    string SourcePath,
    string TargetRelativePath,
    DateTimeOffset CreatedAt);
