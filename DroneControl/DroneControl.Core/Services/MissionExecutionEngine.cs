using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Core.Services;

public class MissionExecutionEngine : IMissionExecutionEngine
{
    private readonly ILogger<MissionExecutionEngine> _logger;
    private readonly IMissionStateRepository _stateRepo;
    private readonly string _sessionId;

    public MissionState CurrentState { get; private set; } = MissionState.Idle;
    public MissionPlan? CurrentPlan { get; private set; }
    public Waypoint? CurrentWaypoint { get; private set; }
    public int ProgressPercentage { get; private set; }

    public event EventHandler<MissionState>? StateChanged;
    public event EventHandler<Waypoint>? WaypointReached;

    public MissionExecutionEngine(ILogger<MissionExecutionEngine> logger, IMissionStateRepository stateRepo, string sessionId)
    {
        _logger = logger;
        _stateRepo = stateRepo;
        _sessionId = sessionId;
    }

    public async Task LoadPlanAsync(MissionPlan plan)
    {
        CurrentPlan = plan;
        ProgressPercentage = 0;
        await TransitionStateAsync(MissionState.Idle, null);
    }

    public async Task StartAsync()
    {
        if (CurrentPlan == null || CurrentPlan.Waypoints.Count == 0)
        {
            await TransitionStateAsync(MissionState.Failed, null);
            return;
        }

        await TransitionStateAsync(MissionState.Starting, null);
        CurrentWaypoint = CurrentPlan.Waypoints[0];
        await TransitionStateAsync(MissionState.Executing, CurrentWaypoint.Id);
    }

    public async Task PauseAsync()
    {
        if (CurrentState == MissionState.Executing)
        {
            await TransitionStateAsync(MissionState.Paused, CurrentWaypoint?.Id);
        }
    }

    public async Task ResumeAsync()
    {
        if (CurrentState == MissionState.Paused)
        {
            await TransitionStateAsync(MissionState.Executing, CurrentWaypoint?.Id);
        }
    }

    public async Task CancelAsync()
    {
        await TransitionStateAsync(MissionState.Cancelled, CurrentWaypoint?.Id);
    }

    public async Task UpdateProgressAsync(Waypoint reached)
    {
        if (CurrentState != MissionState.Executing || CurrentPlan == null) return;

        WaypointReached?.Invoke(this, reached);
        
        int index = -1;
        for (int i = 0; i < CurrentPlan.Waypoints.Count; i++)
        {
            if (CurrentPlan.Waypoints[i] == reached)
            {
                index = i;
                break;
            }
        }
        if (index >= 0 && index < CurrentPlan.Waypoints.Count - 1)
        {
            CurrentWaypoint = CurrentPlan.Waypoints[index + 1];
            ProgressPercentage = (int)(((double)(index + 1) / CurrentPlan.Waypoints.Count) * 100);
            await TransitionStateAsync(MissionState.Executing, CurrentWaypoint.Id);
        }
        else
        {
            ProgressPercentage = 100;
            CurrentWaypoint = null;
            await TransitionStateAsync(MissionState.Completed, null);
        }
    }

    private async Task TransitionStateAsync(MissionState newState, Guid? waypointId)
    {
        CurrentState = newState;
        await _stateRepo.SaveStateTransitionAsync(_sessionId, DateTimeOffset.UtcNow, newState, waypointId);
        StateChanged?.Invoke(this, newState);
    }
}
