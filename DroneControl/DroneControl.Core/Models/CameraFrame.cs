namespace DroneControl.Core.Models;

public sealed record CameraFrame(
    string FrameId,
    DateTimeOffset Timestamp,
    int Width,
    int Height,
    byte[]? ImageBytes = null);
