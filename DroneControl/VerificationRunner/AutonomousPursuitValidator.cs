using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Core.Services;
using DroneControl.Integrations;
using DroneControl.Integrations.MAVSDK;
using DroneControl.Integrations.Tracking;
using DroneControl.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace VerificationRunner;

public sealed class PursuitMockVisionProvider : IVisionProvider
{
    public string ProviderName => "Pursuit Mock Vision";
    public IReadOnlySet<DetectedObjectType> SupportedObjectTypes { get; } = new HashSet<DetectedObjectType> { DetectedObjectType.Person };
    
    public event EventHandler<VisionEvent>? VisionEventEmitted;

    public double TargetLatitude { get; set; }
    public double TargetLongitude { get; set; }

    private readonly IDroneProvider _droneProvider;

    public PursuitMockVisionProvider(IDroneProvider droneProvider)
    {
        _droneProvider = droneProvider;
    }

    public Task<IReadOnlyList<DetectionResult>> DetectAsync(CameraFrame frame, CancellationToken cancellationToken = default)
    {
        var t = _droneProvider.CurrentTelemetry;
        if (t == null || TargetLatitude == 0) return Task.FromResult<IReadOnlyList<DetectionResult>>(Array.Empty<DetectionResult>());

        // Reverse-engineer the CommandPlanner math to place the GPS target into the camera frame
        var nx = (TargetLongitude - t.Longitude) / 0.0002;
        var ny = (t.Latitude - TargetLatitude) / 0.0002;
        
        // If target is too far, clamp it to screen edge
        nx = Math.Clamp(nx, -0.49, 0.49);
        ny = Math.Clamp(ny, -0.49, 0.49);

        var cx = (nx + 0.5) * 1280.0;
        var cy = (ny + 0.5) * 720.0;

        var results = new[] {
            new DetectionResult(
                DetectionId: "pursuit-target-1",
                ObjectType: DetectedObjectType.Person,
                Confidence: 0.95,
                X: cx - 40,
                Y: cy - 80,
                Width: 80,
                Height: 160)
        };

        VisionEventEmitted?.Invoke(this, new VisionEvent(DateTimeOffset.Now, results));
        return Task.FromResult<IReadOnlyList<DetectionResult>>(results);
    }
}

public static class AutonomousPursuitValidator
{
    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 6371e3;
        var phi1 = lat1 * Math.PI / 180;
        var phi2 = lat2 * Math.PI / 180;
        var deltaPhi = (lat2 - lat1) * Math.PI / 180;
        var deltaLambda = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) + Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    public static async Task RunAsync()
    {
        Console.WriteLine("\n========================================================");
        Console.WriteLine("    AUTONOMOUS PURSUIT VERIFICATION AUDIT               ");
        Console.WriteLine("========================================================\n");

        var services = new ServiceCollection();
        services.AddLogging(c => c.AddConsole());
        services.AddDroneControlRuntimeManagement();
        services.Configure<MavsdkProviderOptions>(opt => 
        {
            opt.GrpcAddress = "http://localhost:50051";
            opt.ConnectionMode = ConnectionMode.ExternalEndpoint;
        });
        services.AddPx4MavsdkProviders();
        services.AddSingleton<ITrackingProvider, IouTrackingProvider>();
        services.AddSingleton<ITargetLockService, TargetLockService>();
        services.AddSingleton<ICommandPlanner, CommandPlanner>();
        services.AddSingleton<PursuitMockVisionProvider>();
        services.AddSingleton<IVisionProvider>(p => p.GetRequiredService<PursuitMockVisionProvider>());

        var provider = services.BuildServiceProvider();
        var drone = provider.GetRequiredService<IDroneProvider>();
        var vision = provider.GetRequiredService<PursuitMockVisionProvider>();
        var tracker = provider.GetRequiredService<ITrackingProvider>();
        var lockService = provider.GetRequiredService<ITargetLockService>();
        var planner = provider.GetRequiredService<ICommandPlanner>();

        Console.WriteLine("[Audit] Attempting connection to MAVSDK...");
        await drone.ConnectAsync();
        Console.WriteLine("[Audit] Connected to MAVSDK.");

        await drone.ArmAsync();
        await drone.TakeoffAsync();
        Console.WriteLine("[Audit] Waiting 10s for Takeoff...");
        await Task.Delay(10000);

        var startTelemetry = drone.CurrentTelemetry;
        if (startTelemetry == null) throw new Exception("No telemetry.");

        // Place target ~22 meters North, ~22 meters East of drone
        vision.TargetLatitude = startTelemetry.Latitude + 0.0002;
        vision.TargetLongitude = startTelemetry.Longitude + 0.0002;

        Console.WriteLine($"[Audit] Target spawned at Lat={vision.TargetLatitude:F6}, Lon={vision.TargetLongitude:F6}");

        int frames = 0;
        int successfulGoto = 0;

        for (int i = 0; i < 40; i++) // 40 iterations * 500ms = 20 seconds of pursuit
        {
            frames++;
            // Target is moving slowly away (0.000005 deg per frame ~0.5 meters)
            vision.TargetLatitude += 0.000005;
            vision.TargetLongitude += 0.000005;

            var frame = new CameraFrame($"FRM-{i}", DateTimeOffset.UtcNow, 1280, 720);
            var detections = await vision.DetectAsync(frame);
            var tracks = await tracker.UpdateAsync(detections);
            
            // Auto-lock if not locked
            if (lockService.CurrentLock == null && tracks.Any())
            {
                lockService.RequestLock(tracks.First());
                Console.WriteLine($"[Audit] Target Lock acquired on TRK-{tracks.First().TrackId}");
            }

            lockService.UpdateFromTracks(tracks);
            
            var commands = planner.PlanCommands(null, null, drone.CurrentTelemetry, lockService.CurrentLock);
            var gotoCmd = commands.OfType<DroneCommand.Goto>().FirstOrDefault();
            
            if (gotoCmd != null)
            {
                await drone.GotoAsync(gotoCmd.Latitude, gotoCmd.Longitude, gotoCmd.Altitude);
                successfulGoto++;
            }

            var dist = CalculateDistance(drone.CurrentTelemetry.Latitude, drone.CurrentTelemetry.Longitude, vision.TargetLatitude, vision.TargetLongitude);
            Console.WriteLine($"[T={i*0.5:F1}s] TargetDist: {dist:F2}m | DroneLat: {drone.CurrentTelemetry.Latitude:F6} | DroneLon: {drone.CurrentTelemetry.Longitude:F6} | GotoCmd: {(gotoCmd!=null?"YES":"NO ")}");
            
            await Task.Delay(500);
        }

        var endDist = CalculateDistance(drone.CurrentTelemetry.Latitude, drone.CurrentTelemetry.Longitude, vision.TargetLatitude, vision.TargetLongitude);
        var droneMoved = CalculateDistance(startTelemetry.Latitude, startTelemetry.Longitude, drone.CurrentTelemetry.Latitude, drone.CurrentTelemetry.Longitude);

        Console.WriteLine($"\n[Result] Total frames processed: {frames}");
        Console.WriteLine($"[Result] Total GotoAsync emitted: {successfulGoto}");
        Console.WriteLine($"[Result] Distance drone moved from start: {droneMoved:F2}m");
        Console.WriteLine($"[Result] Final drone-target distance: {endDist:F2}m");

        Console.WriteLine("\n[Audit] RTL...");
        await drone.ReturnToLaunchAsync();
        
        Console.WriteLine("\nDone.");
    }
}
