using System;
using System.Collections.Generic;
using System.Linq;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Core.Services;

public sealed class TargetLockService : ITargetLockService
{
    private readonly ILogger<TargetLockService> _logger;

    public TargetLockService(ILogger<TargetLockService> logger)
    {
        _logger = logger;
    }

    public TargetLock? CurrentLock { get; private set; }
    public event EventHandler<TargetLockEvent>? TargetLockUpdated;

    public void RequestLock(TrackedObject target)
    {
        CurrentLock = new TargetLock(target.TrackId, target.Detection, DateTimeOffset.Now);
        _logger.LogInformation("Target lock acquired on TrackId {TrackId} ({ObjectType}).", target.TrackId, target.Detection.ObjectType);
        TargetLockUpdated?.Invoke(this, new TargetLockEvent(DateTimeOffset.Now, CurrentLock));
    }

    public void ClearLock()
    {
        if (CurrentLock != null)
        {
            CurrentLock = null;
            _logger.LogInformation("Target lock cleared.");
            TargetLockUpdated?.Invoke(this, new TargetLockEvent(DateTimeOffset.Now, null));
        }
    }

    public void UpdateFromTracks(IReadOnlyList<TrackedObject> currentTracks)
    {
        if (CurrentLock == null)
            return;

        var match = currentTracks.FirstOrDefault(t => t.TrackId == CurrentLock.TrackId);
        if (match != null)
        {
            // Update lock with latest detection
            CurrentLock = CurrentLock with { Detection = match.Detection };
            TargetLockUpdated?.Invoke(this, new TargetLockEvent(DateTimeOffset.Now, CurrentLock));
        }
        else
        {
            // Object lost in this frame. For now we just keep the last known detection,
            // or we could clear it if stale for too long.
            // In a real system, we might mark it as stale or count frames lost.
        }
    }
}
