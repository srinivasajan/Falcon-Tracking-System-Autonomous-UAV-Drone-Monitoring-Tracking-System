using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DroneControl.Integrations.Tracking;

public class ByteTrackProvider : ITrackingProvider
{
    public string ProviderName => "ByteTrack Provider";

    public Task<IReadOnlyList<TrackedObject>> UpdateAsync(IReadOnlyList<DetectionResult> detections, CancellationToken cancellationToken = default)
    {
        // Placeholder for future native C# ByteTrack logic.
        // For Phase 4, we use TemporaryMockTrackingProvider for integration testing.
        var results = new List<TrackedObject>();
        return Task.FromResult<IReadOnlyList<TrackedObject>>(results);
    }
}
