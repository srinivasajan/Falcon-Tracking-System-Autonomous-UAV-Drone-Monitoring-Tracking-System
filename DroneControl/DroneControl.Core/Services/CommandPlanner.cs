using System;
using System.Collections.Generic;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

namespace DroneControl.Core.Services;

public class CommandPlanner : ICommandPlanner
{
    // Assumed frame dimensions (pixels). Mock simulator emits 1280x720 frames.
    private const double FrameWidth  = 1280.0;
    private const double FrameHeight = 720.0;

    // GPS step per frame when target is at the edge (~22 m at max deflection)
    private const double GpsStepDegrees = 0.0002;

    public IReadOnlyList<DroneCommand> PlanCommands(
        MissionPlan? plan,
        Waypoint? currentWaypoint,
        TelemetrySnapshot? telemetry,
        TargetLock? targetLock)
    {
        var commands = new List<DroneCommand>();

        if (targetLock != null && telemetry != null)
        {
            // --- Camera tracking command (pitch / yaw) ---
            double pitch = targetLock.Detection.Y > FrameHeight / 2 ? 10 : -10;
            double yaw   = targetLock.Detection.X > FrameWidth  / 2 ? 15 : -15;
            commands.Add(new DroneCommand.TrackTarget(
                Guid.NewGuid(), DateTimeOffset.UtcNow,
                targetLock.TrackId.ToString(), pitch, yaw));

            // --- GPS navigation: move drone toward target centroid ---
            // Normalize centroid to [-0.5, +0.5]
            double cx = targetLock.Detection.X + targetLock.Detection.Width  / 2.0;
            double cy = targetLock.Detection.Y + targetLock.Detection.Height / 2.0;
            double nx = (cx / FrameWidth)  - 0.5;   // positive → target right of centre
            double ny = (cy / FrameHeight) - 0.5;   // positive → target below  centre

            // Apply proportional GPS delta (screen-Y increases downward → south)
            double targetLat = telemetry.Latitude  - ny * GpsStepDegrees;
            double targetLon = telemetry.Longitude + nx * GpsStepDegrees;

            commands.Add(new DroneCommand.Goto(
                Guid.NewGuid(), DateTimeOffset.UtcNow,
                targetLat, targetLon,
                telemetry.AltitudeMeters, 3.0));
        }

        if (currentWaypoint != null && telemetry != null)
        {
            double dist = Distance(
                telemetry.Latitude, telemetry.Longitude,
                currentWaypoint.Latitude, currentWaypoint.Longitude);

            if (dist > 2.0)
            {
                commands.Add(new DroneCommand.Goto(
                    Guid.NewGuid(), DateTimeOffset.UtcNow,
                    currentWaypoint.Latitude, currentWaypoint.Longitude,
                    currentWaypoint.Altitude, 5.0));
            }
            else
            {
                commands.Add(new DroneCommand.Hold(Guid.NewGuid(), DateTimeOffset.UtcNow, 2.0));
            }
        }

        return commands;
    }

    private static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = lat2 - lat1;
        var dLon = lon2 - lon1;
        return Math.Sqrt(dLat * dLat + dLon * dLon) * 111_000;
    }
}
