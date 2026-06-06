using System;
using System.Collections.Generic;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

namespace DroneControl.Core.Services;

public class CommandPlanner : ICommandPlanner
{
    public IReadOnlyList<DroneCommand> PlanCommands(MissionPlan? plan, Waypoint? currentWaypoint, TelemetrySnapshot? telemetry, TargetLock? targetLock)
    {
        var commands = new List<DroneCommand>();

        if (targetLock != null && telemetry != null)
        {
            // Calculate a rough camera pitch/yaw based on the locked object position.
            double pitch = targetLock.Detection.Y > 0.5 ? 10 : -10;
            double yaw = targetLock.Detection.X > 0.5 ? 15 : -15;

            commands.Add(new DroneCommand.TrackTarget(Guid.NewGuid(), DateTimeOffset.UtcNow, targetLock.TrackId.ToString(), pitch, yaw));
        }

        if (currentWaypoint != null && telemetry != null)
        {
            double dist = Distance(telemetry.Latitude, telemetry.Longitude, currentWaypoint.Latitude, currentWaypoint.Longitude);

            if (dist > 2.0) // 2 meters tolerance
            {
                commands.Add(new DroneCommand.Goto(Guid.NewGuid(), DateTimeOffset.UtcNow, currentWaypoint.Latitude, currentWaypoint.Longitude, currentWaypoint.Altitude, 5.0));
            }
            else
            {
                commands.Add(new DroneCommand.Hold(Guid.NewGuid(), DateTimeOffset.UtcNow, 2.0));
            }
        }

        return commands;
    }

    private double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        // Simplistic Euclidean distance approximation for logic testing
        var dLat = lat2 - lat1;
        var dLon = lon2 - lon1;
        return Math.Sqrt(dLat * dLat + dLon * dLon) * 111000; // Roughly convert to meters
    }
}
