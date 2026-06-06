namespace DroneControl.Runtime.Models;

public sealed record RuntimeOutput(
    RuntimeId RuntimeId,
    bool IsError,
    string Text,
    DateTimeOffset Timestamp);
