namespace DroneControl.Core.Models;

public sealed record MissionPlan(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<Waypoint> Waypoints);
