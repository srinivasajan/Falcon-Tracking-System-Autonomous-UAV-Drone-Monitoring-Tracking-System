using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroneControl.Core.Models;
using DroneControl.Integrations.Tracking;

namespace TestRunner;

public static class IouTrackingValidation
{
    public static async Task RunAsync()
    {
        Console.WriteLine("Starting IoU Tracking Validation...");

        var tracker = new IouTrackingProvider();
        
        // Frame 1
        var frame1Detections = new List<DetectionResult>
        {
            new DetectionResult("DET-1", DetectedObjectType.Car, 0.95, 100, 100, 50, 50)
        };
        var frame1Tracks = await tracker.UpdateAsync(frame1Detections);
        
        if (frame1Tracks.Count != 1)
        {
            Console.WriteLine("RESULT: ❌ Failed (Frame 1 Track count)");
            return;
        }
        var trackIdFrame1 = frame1Tracks[0].TrackId;
        Console.WriteLine($"Frame 1: Created TrackId = {trackIdFrame1}");

        // Frame 2 (Object moved slightly)
        var frame2Detections = new List<DetectionResult>
        {
            new DetectionResult("DET-2", DetectedObjectType.Car, 0.92, 110, 110, 50, 50)
        };
        var frame2Tracks = await tracker.UpdateAsync(frame2Detections);

        if (frame2Tracks.Count != 1)
        {
            Console.WriteLine("RESULT: ❌ Failed (Frame 2 Track count)");
            return;
        }
        var trackIdFrame2 = frame2Tracks[0].TrackId;
        Console.WriteLine($"Frame 2: TrackId = {trackIdFrame2}");

        if (trackIdFrame1 == trackIdFrame2)
        {
            Console.WriteLine("RESULT: ✅ Proven (Same object retains TrackId)");
        }
        else
        {
            Console.WriteLine("RESULT: ❌ Failed (TrackId changed)");
        }
    }
}
