namespace DroneControl.Core.Models;

public sealed record TargetLock(
    int TrackId,
    DetectionResult Detection,
    DateTimeOffset LockedAt);
