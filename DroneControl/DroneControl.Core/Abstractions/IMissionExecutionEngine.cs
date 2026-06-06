using System;
using System.Threading.Tasks;
using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface IMissionExecutionEngine
{
    MissionState CurrentState { get; }
    MissionPlan? CurrentPlan { get; }
    Waypoint? CurrentWaypoint { get; }
    int ProgressPercentage { get; }

    event EventHandler<MissionState>? StateChanged;
    event EventHandler<Waypoint>? WaypointReached;

    Task LoadPlanAsync(MissionPlan plan);
    Task StartAsync();
    Task PauseAsync();
    Task ResumeAsync();
    Task CancelAsync();
}
