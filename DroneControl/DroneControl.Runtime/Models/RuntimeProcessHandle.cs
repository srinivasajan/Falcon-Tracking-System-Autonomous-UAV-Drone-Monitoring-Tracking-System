namespace DroneControl.Runtime.Models;

public sealed record RuntimeProcessHandle(
    RuntimeId RuntimeId,
    int ProcessId,
    DateTimeOffset StartedAt);
