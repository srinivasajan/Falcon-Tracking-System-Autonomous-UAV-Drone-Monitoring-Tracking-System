using System;

namespace DroneControl.Core.Models;

public sealed record TrackedObject(
    int TrackId,
    DetectionResult Detection,
    bool IsStale);
