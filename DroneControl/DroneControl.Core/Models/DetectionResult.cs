namespace DroneControl.Core.Models;

public sealed record DetectionResult(
    string DetectionId,
    DetectedObjectType ObjectType,
    double Confidence,
    double X,
    double Y,
    double Width,
    double Height);
