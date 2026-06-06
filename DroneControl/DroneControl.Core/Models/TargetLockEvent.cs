using System;

namespace DroneControl.Core.Models;

public sealed record TargetLockEvent(
    DateTimeOffset Timestamp,
    TargetLock? CurrentLock);
