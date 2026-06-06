using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface ISafetyManager
{
    SafetyLevel CurrentSafetyLevel { get; }
    IReadOnlyList<SafetyEvent> ActiveWarnings { get; }

    event EventHandler<SafetyEvent>? SafetyEventRaised;

    Task EvaluateStateAsync(TelemetrySnapshot telemetry, IReadOnlyList<TrackedObject> tracks, IntegrationHealth visionHealth);
    Task ClearWarningAsync(Guid eventId);
}
