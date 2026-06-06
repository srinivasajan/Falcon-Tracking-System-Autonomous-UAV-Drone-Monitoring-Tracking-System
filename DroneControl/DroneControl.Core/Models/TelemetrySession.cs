namespace DroneControl.Core.Models;

public sealed record TelemetrySession(
    string Id,
    string Name,
    DateTimeOffset StartTime,
    string? VideoPath,
    DateTimeOffset? EndTime);
