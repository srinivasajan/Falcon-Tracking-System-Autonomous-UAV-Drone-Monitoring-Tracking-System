namespace DroneControl.Runtime.Models;

public sealed record RuntimeExit(
    RuntimeId RuntimeId,
    int ExitCode,
    DateTimeOffset ExitedAt);
