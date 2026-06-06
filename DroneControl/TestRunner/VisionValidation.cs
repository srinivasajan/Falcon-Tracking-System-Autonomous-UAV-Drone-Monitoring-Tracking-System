using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Integrations.Mocks;
using DroneControl.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace TestRunner;

public static class VisionValidation
{
    public static async Task RunAsync()
    {
        Console.WriteLine("Starting Vision Architecture Validation...");

        var services = new ServiceCollection();
        services.AddSingleton<StoragePaths>();
        services.AddDroneControlStorage();
        services.AddSingleton<IVisionProvider, TemporaryMockVisionProvider>();
        
        var provider = services.BuildServiceProvider();

        var paths = provider.GetRequiredService<StoragePaths>();
        paths.EnsureCreated();

        var detectionRepo = provider.GetRequiredService<IDetectionRepository>();
        var visionProvider = provider.GetRequiredService<IVisionProvider>();

        // 1. Programmatically inject a fake CameraFrame via the Mock provider
        var timestamp = DateTimeOffset.UtcNow;
        var frame = new CameraFrame("frame_123", timestamp, 1920, 1080, new byte[0]);
        var sessionId = "Vision_Test_Session";

        Console.WriteLine("Detecting objects in frame...");
        var detections = await visionProvider.DetectAsync(frame);
        
        Console.WriteLine($"Detected {detections.Count} objects. Saving to SQLite...");
        await detectionRepo.SaveDetectionsAsync(sessionId, timestamp, detections);

        // 2. Verify SQLite rows written
        var dbPath = Path.Combine(paths.RootPath, "dronecontrol.db");
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        
        var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM vision_detections WHERE session_id = 'Vision_Test_Session';";
        var detectionCount = (long)(await countCmd.ExecuteScalarAsync() ?? 0);

        Console.WriteLine($"DB_VERIFICATION: SavedDetections={detectionCount}");

        // 3. Verify Load
        var loaded = await detectionRepo.LoadDetectionsAsync(sessionId, timestamp);
        Console.WriteLine($"LOADED_DETECTIONS: {loaded.Count}");
        foreach (var det in loaded)
        {
            Console.WriteLine($"  -> {det.DetectionId}: {det.ObjectType} ({det.Confidence:P0})");
        }

        if (detectionCount == detections.Count && loaded.Count == detections.Count)
        {
            Console.WriteLine("RESULT: ✅ Proven");
        }
        else
        {
            Console.WriteLine("RESULT: ❌ Failed");
        }
    }
}
