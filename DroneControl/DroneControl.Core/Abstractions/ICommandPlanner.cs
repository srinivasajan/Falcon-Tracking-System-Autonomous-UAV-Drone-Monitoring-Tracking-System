using System.Collections.Generic;
using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface ICommandPlanner
{
    /// <summary>
    /// Converts current mission state, telemetry, and tracking locks into concrete drone commands.
    /// </summary>
    IReadOnlyList<DroneCommand> PlanCommands(
        MissionPlan? plan,
        Waypoint? currentWaypoint,
        TelemetrySnapshot? telemetry,
        TargetLock? targetLock);
}
