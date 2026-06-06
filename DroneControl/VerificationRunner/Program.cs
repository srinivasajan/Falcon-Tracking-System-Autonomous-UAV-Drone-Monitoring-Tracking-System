using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Core.Services;
using DroneControl.Integrations.Mocks;
using DroneControl.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Microsoft.Extensions.Logging;

namespace VerificationRunner;

class Program
{
    static async Task Main(string[] args)
    {
        // Phase 1 Req 6: Live SITL GotoAsync validation
        if (args.Length > 0 && args[0] == "--sitl")
        {
            await AutonomousPursuitValidator.RunAsync();
            return;
        }

        Console.WriteLine("=== PHASE 7, 8, 9 ZERO TRUST VALIDATION ===");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<StoragePaths>();
        services.AddDroneControlStorage();
        var provider = services.BuildServiceProvider();
        
        var paths = provider.GetRequiredService<StoragePaths>();
        paths.EnsureCreated();
        var db = provider.GetRequiredService<SqliteDatabase>();
        await db.InitializeAsync();

        // -----------------------------------------
        // PHASE 7: REPLAY VALIDATION
        // -----------------------------------------
        Console.WriteLine("\n[PHASE 7: REPLAY VALIDATION]");
        var recorder = provider.GetRequiredService<ITelemetryRecorder>();
        var sessionId = await recorder.StartRecordingAsync("ReplayTest");
        for (int i = 0; i < 5; i++) {
            await recorder.RecordAsync(new TelemetrySnapshot(DateTimeOffset.UtcNow.AddSeconds(i), true, 100, 45, -120, 10, i, "Flying"));
        }
        await recorder.StopRecordingAsync();
        if (recorder is IAsyncDisposable ad) await ad.DisposeAsync();

        var replayProvider = new ReplayDroneProvider(provider.GetRequiredService<ITelemetryRecorder>(), sessionId, NullLogger<ReplayDroneProvider>.Instance);
        
        int eventsEmitted = 0;
        replayProvider.TelemetryUpdated += (s, e) => eventsEmitted++;
        
        var sw = Stopwatch.StartNew();
        await replayProvider.ConnectAsync();
        await Task.Delay(6000); // Wait for 5 second replay
        sw.Stop();
        
        Console.WriteLine($"[Result] Events Emitted: {eventsEmitted}");
        Console.WriteLine($"[Result] Timing Accuracy (Expected ~4-5s): {sw.ElapsedMilliseconds / 1000.0}s");
        Console.WriteLine($"[Result] Replay Completion: {(eventsEmitted == 5 ? "SUCCESS" : "FAILED")}");

        // -----------------------------------------
        // PHASE 8: SAFETY VALIDATION
        // -----------------------------------------
        Console.WriteLine("\n[PHASE 8: SAFETY VALIDATION]");
        var eventRepo = provider.GetRequiredService<ISafetyEventRepository>();
        var safety = new SafetyManager(NullLogger<SafetyManager>.Instance, eventRepo, "SafetyTest");
        
        var tracks = new List<TrackedObject>();
        var health = new IntegrationHealth(true, "Vision", "1.0", "OK");

        Console.WriteLine("[Test] Triggering Low Battery (29%)");
        await safety.EvaluateStateAsync(new TelemetrySnapshot(DateTimeOffset.UtcNow, true, 29, 0, 0, 0, 0, "Flying"), tracks, health);
        Console.WriteLine($"Level: {safety.CurrentSafetyLevel}");

        Console.WriteLine("[Test] Triggering Telemetry Timeout");
        await safety.EvaluateStateAsync(new TelemetrySnapshot(DateTimeOffset.UtcNow.AddSeconds(-5), true, 100, 0, 0, 0, 0, "Flying"), tracks, health);
        Console.WriteLine($"Level: {safety.CurrentSafetyLevel}");

        Console.WriteLine("[Test] Triggering GPS Loss");
        await safety.EvaluateStateAsync(new TelemetrySnapshot(DateTimeOffset.UtcNow, true, 100, 0, 0, 0, 0, "NoGPS"), tracks, health);
        Console.WriteLine($"Level: {safety.CurrentSafetyLevel}");

        Console.WriteLine($"[Result] Total Active Warnings: {safety.ActiveWarnings.Count}");
        var rtlFired = safety.ActiveWarnings.Any(w => w.Level == SafetyLevel.RTL);
        Console.WriteLine($"[Result] RTL Event Fired: {rtlFired}");

        // -----------------------------------------
        // PHASE 9: ENDURANCE TEST
        // -----------------------------------------
        Console.WriteLine("\n[PHASE 9: ENDURANCE TEST]");
        var stateRepo = provider.GetRequiredService<IMissionStateRepository>();
        var engine = new MissionExecutionEngine(NullLogger<MissionExecutionEngine>.Instance, stateRepo, "Endurance");
        
        async Task RunMission(int wpCount)
        {
            var waypoints = Enumerable.Range(0, wpCount).Select(i => new Waypoint { Sequence = i, Latitude = 0, Longitude = 0, Altitude = 10 }).ToList();
            var plan = new MissionPlan("m1", $"Mission-{wpCount}", DateTimeOffset.UtcNow, waypoints);
            await engine.LoadPlanAsync(plan);
            await engine.StartAsync();
            
            var esw = Stopwatch.StartNew();
            while (engine.CurrentState == MissionState.Executing)
            {
                if (engine.CurrentWaypoint != null)
                    await engine.UpdateProgressAsync(engine.CurrentWaypoint);
            }
            esw.Stop();
            var mem = Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0);
            Console.WriteLine($"Mission {wpCount} WP: State={engine.CurrentState}, Time={esw.ElapsedMilliseconds}ms, Mem={mem:F2}MB");
        }

        await RunMission(10);
        await RunMission(100);
    }
}

