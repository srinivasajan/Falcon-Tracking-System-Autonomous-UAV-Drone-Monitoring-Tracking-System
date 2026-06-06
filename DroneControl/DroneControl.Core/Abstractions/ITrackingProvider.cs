using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface ITrackingProvider
{
    string ProviderName { get; }
    Task<IReadOnlyList<TrackedObject>> UpdateAsync(IReadOnlyList<DetectionResult> detections, CancellationToken cancellationToken = default);
}
