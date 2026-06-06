using DroneControl.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DroneControl.Core.Abstractions;

public interface ITrackingRepository
{
    Task SaveTracksAsync(string sessionId, DateTimeOffset timestamp, IReadOnlyList<TrackedObject> tracks, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackedObject>> LoadTracksAsync(string sessionId, DateTimeOffset timestamp, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<DateTimeOffset, IReadOnlyList<TrackedObject>>> LoadAllTracksForSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
