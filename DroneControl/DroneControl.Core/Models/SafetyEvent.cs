using System;

namespace DroneControl.Core.Models;

public enum SafetyLevel
{
    Info,
    Warning,
    Abort,
    RTL
}

public record SafetyEvent(
    Guid Id,
    DateTimeOffset Timestamp,
    SafetyLevel Level,
    string Source,
    string Description);
