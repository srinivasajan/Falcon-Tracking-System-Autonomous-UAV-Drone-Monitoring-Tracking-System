using DroneControl.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DroneControl.Core.Abstractions;

public interface IDetectionRepository
{
    Task SaveDetectionsAsync(string sessionId, DateTimeOffset timestamp, IReadOnlyList<DetectionResult> detections, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DetectionResult>> LoadDetectionsAsync(string sessionId, DateTimeOffset timestamp, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<DateTimeOffset, IReadOnlyList<DetectionResult>>> LoadAllDetectionsForSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
