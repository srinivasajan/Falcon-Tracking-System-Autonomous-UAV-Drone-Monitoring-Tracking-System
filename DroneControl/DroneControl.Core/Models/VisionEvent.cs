namespace DroneControl.Core.Models;
using System;
using System.Collections.Generic;

public sealed record VisionEvent(
    DateTimeOffset Timestamp,
    IReadOnlyList<DetectionResult> Detections);
