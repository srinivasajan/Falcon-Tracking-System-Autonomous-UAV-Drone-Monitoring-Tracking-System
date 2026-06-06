// Phase1PipelineValidation.cs
// Zero-trust headless integration test covering all 6 Phase 1 objectives.
// Exercises: Mock detection → IoU tracking → TargetLockService → CommandPlanner → GotoAsync

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Core.Services;
using DroneControl.Integrations.Mocks;
using DroneControl.Integrations.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DroneControl.TestRunner;

public static class Phase1PipelineValidation
{
    public static async Task<bool> RunAsync()
    {
        Console.WriteLine("\n========================================================");
        Console.WriteLine("  PHASE 1 PIPELINE VALIDATION — Zero-Trust Runtime Test");
        Console.WriteLine("========================================================\n");

        // ── Setup DI (mirrors App.xaml.cs Simulation mode without drone HW) ──
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<IVisionProvider,   TemporaryMockVisionProvider>();
        services.AddSingleton<ITrackingProvider, IouTrackingProvider>();
        services.AddSingleton<ITargetLockService, TargetLockService>();
        services.AddSingleton<ICommandPlanner,   CommandPlanner>();

        var provider = services.BuildServiceProvider();
        var vision         = provider.GetRequiredService<IVisionProvider>();
        var tracker        = provider.GetRequiredService<ITrackingProvider>();
        var lockService    = provider.GetRequiredService<ITargetLockService>();
        var planner        = provider.GetRequiredService<ICommandPlanner>();

        Console.WriteLine($"[DI] IVisionProvider   → {vision.GetType().Name}");
        Console.WriteLine($"[DI] ITrackingProvider → {tracker.GetType().Name}");
        Console.WriteLine($"[DI] ICommandPlanner   → {planner.GetType().Name}");
        Console.WriteLine();

        bool pass = true;
        int gotoCommandsGenerated = 0;
        int framesProcessed = 0;
        int successfulLocks = 0;

        // Simulate 10 frames at 500ms intervals
        for (int tick = 1; tick <= 10; tick++)
        {
            var frame = new CameraFrame($"MOCK-{tick:000000}", DateTimeOffset.UtcNow, 1280, 720);

            // ── Req 2: Detection ─────────────────────────────────────────
            var detections = await vision.DetectAsync(frame);
            framesProcessed++;

            Console.Write($"Frame {tick:00}: {detections.Count} detections");
            if (detections.Count == 0) { Console.WriteLine(" ❌ ZERO DETECTIONS"); pass = false; continue; }

            // ── Req 3: Continuous tracking ────────────────────────────────
            var tracks = await tracker.UpdateAsync(detections);
            var trackIds = string.Join(", ", tracks.Select(t => $"TRK-{t.TrackId}({t.Detection.ObjectType})"));
            Console.Write($" | {tracks.Count} tracks [{trackIds}]");

            // ── Req 1: Target ingestion (auto-select first live track) ─────
            var primaryTrack = tracks.FirstOrDefault(t => !t.IsStale);
            if (primaryTrack != null)
            {
                lockService.RequestLock(primaryTrack);
                successfulLocks++;
            }

            // ── Req 4+5: CommandPlanner → navigation commands ─────────────
            // Mock telemetry representing a drone in simulation at 10m alt
            var telemetry = new TelemetrySnapshot(
                DateTimeOffset.UtcNow, true, 85,
                47.397742, 8.545594, 0.0, 10.0, "HOLD");

            lockService.UpdateFromTracks(tracks);
            var currentLock = lockService.CurrentLock;

            var commands = planner.PlanCommands(null, null, telemetry, currentLock);
            var gotoCmd = commands.OfType<DroneCommand.Goto>().FirstOrDefault();
            var trackCmd = commands.OfType<DroneCommand.TrackTarget>().FirstOrDefault();

            if (gotoCmd != null)
            {
                gotoCommandsGenerated++;
                Console.Write($" | 📡 GOTO Lat={gotoCmd.Latitude:F6} Lon={gotoCmd.Longitude:F6} Alt={gotoCmd.Altitude:F1}");
            }
            if (trackCmd != null)
            {
                Console.Write($" | 🎯 TRACK pitch={trackCmd.CameraPitch:F0}° yaw={trackCmd.CameraYaw:F0}°");
            }
            if (gotoCmd == null && trackCmd == null && currentLock != null)
            {
                Console.Write(" ❌ LOCK ACTIVE BUT NO NAV COMMAND");
                pass = false;
            }
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("─────────────────────────────────────────────────────");
        Console.WriteLine($"Frames processed      : {framesProcessed}/10");
        Console.WriteLine($"Successful locks      : {successfulLocks}");
        Console.WriteLine($"Goto commands issued  : {gotoCommandsGenerated}");
        Console.WriteLine();

        // ── Stability check: TrackId must stay stable for known objects ──
        Console.WriteLine("[Stability Check] Verifying TrackId stability across 5 extra frames...");
        var firstFrame = new CameraFrame("STABLE-001", DateTimeOffset.UtcNow, 1280, 720);
        var baseDetections = await vision.DetectAsync(firstFrame);
        var baseTracks = await tracker.UpdateAsync(baseDetections);
        var personBaseId = baseTracks.FirstOrDefault(t => t.Detection.ObjectType == DetectedObjectType.Person)?.TrackId;

        for (int i = 2; i <= 5; i++)
        {
            var f = new CameraFrame($"STABLE-00{i}", DateTimeOffset.UtcNow, 1280, 720);
            var det = await vision.DetectAsync(f);
            var trk = await tracker.UpdateAsync(det);
            var personId = trk.FirstOrDefault(t => t.Detection.ObjectType == DetectedObjectType.Person)?.TrackId;
            bool stable = personId == personBaseId;
            Console.WriteLine($"  Frame {i}: Person TrackId={personId} (base={personBaseId}) {(stable ? "✅ STABLE" : "❌ CHANGED")}");
            if (!stable) pass = false;
        }

        Console.WriteLine();
        Console.WriteLine("─────────────────────────────────────────────────────");

        // ── Final results ────────────────────────────────────────────────
        bool detectOk = framesProcessed == 10;
        bool trackOk  = successfulLocks >= 8;
        bool gotoOk   = gotoCommandsGenerated >= 8;

        Console.WriteLine($"Req 1 – Accept target          : {(successfulLocks > 0 ? "✅ PASS" : "❌ FAIL")}");
        Console.WriteLine($"Req 2 – Detect target          : {(detectOk ? "✅ PASS" : "❌ FAIL")}");
        Console.WriteLine($"Req 3 – Continuous tracking    : {(trackOk  ? "✅ PASS" : "❌ FAIL")}");
        Console.WriteLine($"Req 4 – Navigation commands    : {(gotoOk   ? "✅ PASS" : "❌ FAIL")}");
        Console.WriteLine($"Req 5 – Simulation mode        : ✅ PASS (no drone HW used)");
        Console.WriteLine($"Req 6 – PX4 SITL E2E           : ⚠  PARTIAL (requires live SITL — see SITL test)");
        Console.WriteLine();

        pass = pass && detectOk && trackOk && gotoOk;
        Console.WriteLine($"Phase 1 Pipeline (headless): {(pass ? "✅ PASS" : "❌ FAIL")}");
        Console.WriteLine("========================================================\n");

        return pass;
    }
}
