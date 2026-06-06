using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface IVisionProvider
{
    string ProviderName { get; }
    IReadOnlySet<DetectedObjectType> SupportedObjectTypes { get; }
    event EventHandler<VisionEvent>? VisionEventEmitted;
    Task<IReadOnlyList<DetectionResult>> DetectAsync(CameraFrame frame, CancellationToken cancellationToken = default);
}
