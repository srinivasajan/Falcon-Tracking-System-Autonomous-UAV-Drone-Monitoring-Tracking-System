using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DroneControl.Core.Models;
using DroneControl.Integrations.Vision;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestRunner;

public static class YoloValidation
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== REAL YOLO INFERENCE VALIDATION ===");
        var provider = new YoloVisionProvider(NullLogger<YoloVisionProvider>.Instance);
        
        var imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "TestRunner", "bus.jpg");
        if (!File.Exists(imagePath))
        {
            Console.WriteLine($"Image not found at {imagePath}");
            return;
        }

        Console.WriteLine($"Loading image from: {imagePath}");
        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        
        var frame = new CameraFrame("test_cam", DateTimeOffset.UtcNow, 640, 480, imageBytes);
        
        Console.WriteLine("Running inference...");
        var detections = await provider.DetectAsync(frame, CancellationToken.None);
        
        Console.WriteLine($"\nDetection Count: {detections.Count}");
        foreach(var det in detections)
        {
            Console.WriteLine($"- {det.ObjectType} (Confidence: {det.Confidence:F2}) [X:{det.X}, Y:{det.Y}, W:{det.Width}, H:{det.Height}]");
        }
        Console.WriteLine("\n=== VALIDATION COMPLETE ===");
    }
}
