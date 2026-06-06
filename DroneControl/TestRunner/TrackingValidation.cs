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
using Microsoft.Extensions.Logging;

namespace TestRunner;

public static class TrackingValidation
{
    public static async Task RunAsync()
    {
        Console.WriteLine("Starting Tracking Architecture Validation...");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<StoragePaths>();
        services.AddDroneControlStorage();
        services.AddSingleton<IVisionProvider, TemporaryMockVisionProvider>();
        services.AddSingleton<ITrackingProvider, TemporaryMockTrackingProvider>();
        
        var provider = services.BuildServiceProvider();

        var paths = provider.GetRequiredService<StoragePaths>();
        paths.EnsureCreated();

        var detectionRepo = provider.GetRequiredService<IDetectionRepository>();
        var trackingRepo = provider.GetRequiredService<ITrackingRepository>();
        var visionProvider = provider.GetRequiredService<IVisionProvider>();
        var trackingProvider = provider.GetRequiredService<ITrackingProvider>();

        // 1. Programmatically inject a fake CameraFrame
        var timestamp = DateTimeOffset.UtcNow;
        var frame = new CameraFrame("frame_123", timestamp, 1920, 1080, new byte[0]);
        var sessionId = "Tracking_Test_Session";

        Console.WriteLine("Detecting objects in frame...");
        var detections = await visionProvider.DetectAsync(frame);
        
        Console.WriteLine("Tracking objects...");
        var tracks = await trackingProvider.UpdateAsync(detections);

        Console.WriteLine($"Detected {detections.Count} objects and {tracks.Count} tracks. Saving to SQLite...");
        await detectionRepo.SaveDetectionsAsync(sessionId, timestamp, detections);
        await trackingRepo.SaveTracksAsync(sessionId, timestamp, tracks);

        // 2. Verify SQLite rows written
        var dbPath = Path.Combine(paths.RootPath, "dronecontrol.db");
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        
        var countCmd = connection.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM vision_tracks WHERE session_id = 'Tracking_Test_Session';";
        var trackCount = (long)(await countCmd.ExecuteScalarAsync() ?? 0);

        Console.WriteLine($"DB_VERIFICATION: SavedTracks={trackCount}");

        // 3. Verify Load
        var loaded = await trackingRepo.LoadTracksAsync(sessionId, timestamp);
        Console.WriteLine($"LOADED_TRACKS: {loaded.Count}");
        foreach (var t in loaded)
        {
            Console.WriteLine($"  -> Track ID: {t.TrackId}, Detection: {t.Detection.DetectionId}, ObjectType: {t.Detection.ObjectType}");
        }

        if (trackCount == tracks.Count && loaded.Count == tracks.Count)
        {
            Console.WriteLine("RESULT: ✅ Proven");
        }
        else
        {
            Console.WriteLine("RESULT: ❌ Failed");
        }
    }
}
