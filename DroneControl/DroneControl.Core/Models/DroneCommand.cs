using System;

namespace DroneControl.Core.Models;

public abstract record DroneCommand(Guid Id, DateTimeOffset CreatedAt)
{
    public record Goto(Guid Id, DateTimeOffset CreatedAt, double Latitude, double Longitude, double Altitude, double Speed) : DroneCommand(Id, CreatedAt);
    public record Hold(Guid Id, DateTimeOffset CreatedAt, double DurationSeconds) : DroneCommand(Id, CreatedAt);
    public record Yaw(Guid Id, DateTimeOffset CreatedAt, double HeadingDegrees) : DroneCommand(Id, CreatedAt);
    public record Orbit(Guid Id, DateTimeOffset CreatedAt, double Latitude, double Longitude, double Radius, double Speed) : DroneCommand(Id, CreatedAt);
    public record TrackTarget(Guid Id, DateTimeOffset CreatedAt, string TargetId, double CameraPitch, double CameraYaw) : DroneCommand(Id, CreatedAt);
}
