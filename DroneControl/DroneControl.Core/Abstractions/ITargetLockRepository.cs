using DroneControl.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DroneControl.Core.Abstractions;

public interface ITargetLockRepository
{
    Task SaveTargetLockEventAsync(string sessionId, TargetLockEvent lockEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<DateTimeOffset, TargetLockEvent>> LoadAllTargetLocksForSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
