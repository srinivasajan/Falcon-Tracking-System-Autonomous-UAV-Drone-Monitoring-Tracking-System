using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Integrations.Mocks;

/// <summary>
/// Temporary development mock only. Production tracking should use ByteTrack or DeepSORT.
/// </summary>
public sealed class TemporaryMockTrackingProvider : ITrackingProvider
{
    private readonly ILogger<TemporaryMockTrackingProvider> _logger;

    public TemporaryMockTrackingProvider(ILogger<TemporaryMockTrackingProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "Temporary Mock Tracking";
    private int _nextTrackId = 1;

    public Task<IReadOnlyList<TrackedObject>> UpdateAsync(IReadOnlyList<DetectionResult> detections, CancellationToken cancellationToken = default)
    {
        var results = new List<TrackedObject>();
        foreach (var det in detections)
        {
            results.Add(new TrackedObject(_nextTrackId++, det, false));
        }
        return Task.FromResult<IReadOnlyList<TrackedObject>>(results);
    }
}
