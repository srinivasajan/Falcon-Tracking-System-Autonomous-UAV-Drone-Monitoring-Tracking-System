using System;
using System.Collections.Generic;
using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface ITargetLockService
{
    TargetLock? CurrentLock { get; }
    event EventHandler<TargetLockEvent>? TargetLockUpdated;

    void RequestLock(TrackedObject target);
    void ClearLock();
    void UpdateFromTracks(IReadOnlyList<TrackedObject> currentTracks);
}
