using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

namespace DroneControl.Integrations.Mocks;

/// <summary>
/// Temporary development mock only. Production object detection should use Ultralytics YOLO.
/// </summary>
public sealed class TemporaryMockVisionProvider : IVisionProvider
{
    private static readonly IReadOnlySet<DetectedObjectType> ObjectTypes = new HashSet<DetectedObjectType>
    {
        DetectedObjectType.Person,
        DetectedObjectType.Car,
        DetectedObjectType.Truck,
        DetectedObjectType.Bicycle,
        DetectedObjectType.Motorcycle
    };

    public string ProviderName => "Temporary Mock Vision";
    public IReadOnlySet<DetectedObjectType> SupportedObjectTypes => ObjectTypes;
    
    public event EventHandler<VisionEvent>? VisionEventEmitted;

    public Task<IReadOnlyList<DetectionResult>> DetectAsync(CameraFrame frame, CancellationToken cancellationToken = default)
    {
        var frameNumber = ExtractFrameNumber(frame.FrameId);
        var results = ObjectTypes.Select((type, index) =>
        {
            var x = 90 + index * 190 + Math.Sin((frameNumber + index) / 6d) * 20;
            var y = 95 + index * 60 + Math.Cos((frameNumber + index) / 7d) * 16;
            return new DetectionResult(
                DetectionId: $"{frame.FrameId}-{type}",
                ObjectType: type,
                Confidence: Math.Clamp(0.72 + Math.Sin((frameNumber + index) / 10d) * 0.2, 0.55, 0.97),
                X: x,
                Y: y,
                Width: type is DetectedObjectType.Truck ? 150 : 95,
                Height: type is DetectedObjectType.Person ? 165 : 82);
        }).ToArray();
        VisionEventEmitted?.Invoke(this, new VisionEvent(DateTimeOffset.Now, results));
        return Task.FromResult<IReadOnlyList<DetectionResult>>(results);
    }

    private static int ExtractFrameNumber(string frameId)
    {
        var digits = new string(frameId.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }
}
