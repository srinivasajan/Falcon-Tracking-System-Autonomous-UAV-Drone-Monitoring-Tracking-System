using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Core.Services;
using DroneControl.Storage;
using DroneControl.UI.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Unosquare.FFME;

namespace TestRunner;

public static class UnifiedReplayValidation
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== UNIFIED REPLAY VALIDATION ===");
        
        var paths = new StoragePaths();
        var db = new SqliteDatabase(paths);
        await db.InitializeAsync();
        
        var telemetryRepo = new SqliteTelemetryRecorder(db);
        var detectionRepo = new SqliteDetectionRepository(db);
        var trackingRepo = new SqliteTrackingRepository(db, detectionRepo);
        var lockRepo = new SqliteTargetLockRepository(db, trackingRepo);

        var sessionId = $"TestSession_{Guid.NewGuid():N}";
        Console.WriteLine($"Starting recording session: {sessionId}");
        
        await telemetryRepo.StartRecordingAsync(sessionId, null);
        
        var baseTime = DateTimeOffset.Now;
        
        // Record 5 snapshots, 1 second apart
        for (int i = 0; i < 5; i++)
        {
            var timestamp = baseTime.AddSeconds(i);
            
            // 1. Record Telemetry
            var t = new TelemetrySnapshot(timestamp, true, 100 - i, 45.0, -120.0, 5.0, 10.0, "Auto");
            await telemetryRepo.RecordAsync(t);
            
            // 2. Record Detections (1 person moving)
            var detection = new DetectionResult($"det_{i}", DetectedObjectType.Person, 0.9, i * 10, i * 10, 50, 100);
            await detectionRepo.SaveDetectionsAsync(sessionId, timestamp, new[] { detection });
            
            // 3. Record Tracks (same person, id = 42)
            var track = new TrackedObject(42, detection, false);
            await trackingRepo.SaveTracksAsync(sessionId, timestamp, new[] { track });
            
            // 4. Record Target Lock (Locked on frame 2, cleared on frame 4)
            if (i == 2)
            {
                var lockEvent = new TargetLockEvent(timestamp, new TargetLock(42, detection, timestamp));
                await lockRepo.SaveTargetLockEventAsync(sessionId, lockEvent);
            }
            else if (i == 4)
            {
                var clearEvent = new TargetLockEvent(timestamp, null);
                await lockRepo.SaveTargetLockEventAsync(sessionId, clearEvent);
            }
        }
        
        await telemetryRepo.StopRecordingAsync();
        Console.WriteLine("Recording complete.\n");
        
        Console.WriteLine("Loading session for replay...");
        var snapshots = await telemetryRepo.LoadReplayAsync(sessionId);
        var detections = await detectionRepo.LoadAllDetectionsForSessionAsync(sessionId);
        var tracks = await trackingRepo.LoadAllTracksForSessionAsync(sessionId);
        var locks = await lockRepo.LoadAllTargetLocksForSessionAsync(sessionId);
        
        Console.WriteLine($"Loaded {snapshots.Count} snapshots.");
        Console.WriteLine($"Loaded {detections.Count} detection frames.");
        Console.WriteLine($"Loaded {tracks.Count} track frames.");
        Console.WriteLine($"Loaded {locks.Count} lock events.\n");
        
        using var engine = new TelemetryReplayEngine();
        engine.LoadVisionData(detections, tracks, locks);
        engine.LoadSnapshots(snapshots);
        
        Console.WriteLine("Simulating Replay Engine Scrubbing...");
        
        for (int i = 0; i < 5; i++)
        {
            engine.Seek(TimeSpan.FromSeconds(i));
            
            Console.WriteLine($"Time: {engine.CurrentPosition.TotalSeconds}s");
            Console.WriteLine($"  Telemetry Battery: {(engine.CurrentDetections.Count > 0 ? "OK" : "NO")}");
            
            if (engine.CurrentDetections.Count > 0)
                Console.WriteLine($"  Detections: {engine.CurrentDetections[0].ObjectType} at X={engine.CurrentDetections[0].X}");
                
            if (engine.CurrentTracks.Count > 0)
                Console.WriteLine($"  Tracks: TrackId {engine.CurrentTracks[0].TrackId}");
                
            if (engine.CurrentTargetLock != null)
                Console.WriteLine($"  Lock: Active on TrackId {engine.CurrentTargetLock.TrackId}");
            else
                Console.WriteLine($"  Lock: None");
                
            Console.WriteLine();
        }
        
        Console.WriteLine("Validation Complete.");
    }
}
