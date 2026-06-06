using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface IMissionStateRepository
{
    Task SaveStateTransitionAsync(string sessionId, DateTimeOffset timestamp, MissionState newState, Guid? currentWaypointId);
    Task<IEnumerable<(DateTimeOffset Timestamp, MissionState State, Guid? WaypointId)>> LoadStateTransitionsAsync(string sessionId);
}
