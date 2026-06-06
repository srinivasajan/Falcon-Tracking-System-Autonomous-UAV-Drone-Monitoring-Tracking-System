using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Integrations.PX4;
using DroneControl.Integrations.MAVSDK;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DroneControl.Integrations;
using DroneControl.Runtime;

namespace VerificationRunner;

public static class RuntimeValidator
{
    private static TelemetrySnapshot? _latestTelemetry;

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        var R = 6371e3; // metres
        var phi1 = lat1 * Math.PI / 180; // phi, lambda in radians
        var phi2 = lat2 * Math.PI / 180;
        var deltaPhi = (lat2 - lat1) * Math.PI / 180;
        var deltaLambda = (lon2 - lon1) * Math.PI / 180;

        var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                Math.Cos(phi1) * Math.Cos(phi2) *
                Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return R * c; // in metres
    }

    public static async Task RunValidationsAsync()
    {
        Console.WriteLine("\n=========================================");
        Console.WriteLine("    ZERO-TRUST RUNTIME VALIDATION        ");
        Console.WriteLine("=========================================\n");

        var services = new ServiceCollection();
        services.AddLogging(c => c.AddConsole());
        services.AddDroneControlRuntimeManagement();
        services.Configure<MavsdkProviderOptions>(opt => 
        {
            opt.GrpcAddress = "http://localhost:50051";
            opt.ConnectionMode = ConnectionMode.ExternalEndpoint;
        });
        services.AddPx4MavsdkProviders(); // This registers MavsdkOptions and MavsdkProvider correctly
        
        var provider = services.BuildServiceProvider();
        var drone = provider.GetRequiredService<IDroneProvider>();

        drone.TelemetryUpdated += (s, t) => _latestTelemetry = t;

        Console.WriteLine("[Audit] Attempting connection to MAVSDK...");
        await drone.ConnectAsync();
        Console.WriteLine("[Audit] Connected to MAVSDK.");

        // Wait for telemetry
        for(int i=0; i<50; i++) {
            if (_latestTelemetry != null) break;
            await Task.Delay(100);
        }

        // 1. GotoAsync Validation
        Console.WriteLine("\n--- 1. GotoAsync Validation ---");
        await drone.ArmAsync();
        await drone.TakeoffAsync();
        await Task.Delay(15000); // Wait for takeoff to complete

        var currentTelemetry = _latestTelemetry;
        if (currentTelemetry == null) throw new Exception("No telemetry received.");
        
        var startLat = currentTelemetry.Latitude;
        var startLon = currentTelemetry.Longitude;
        Console.WriteLine($"[Result] Start Position: Lat={startLat}, Lon={startLon}");

        var targetLat = startLat + 0.0001; // Approx 11 meters
        var targetLon = startLon + 0.0001;
        Console.WriteLine($"[Audit] Executing GotoAsync to Lat={targetLat}, Lon={targetLon}...");
        
        await drone.GotoAsync(targetLat, targetLon, currentTelemetry.AltitudeMeters);
        
        // Wait for movement
        await Task.Delay(15000);
        
        var endTelemetry = _latestTelemetry;
        if (endTelemetry == null) throw new Exception("No telemetry received.");
        var endLat = endTelemetry.Latitude;
        var endLon = endTelemetry.Longitude;
        Console.WriteLine($"[Result] End Position: Lat={endLat}, Lon={endLon}");

        var distanceMoved = CalculateDistance(startLat, startLon, endLat, endLon);
        Console.WriteLine($"[Result] Distance Moved: {distanceMoved:F2} meters");
        if (distanceMoved > 5.0)
        {
            Console.WriteLine("✅ GotoAsync Validation PASSED");
        }
        else
        {
            Console.WriteLine("❌ GotoAsync Validation FAILED");
        }

        // 2. ReturnToLaunch Validation
        Console.WriteLine("\n--- 2. ReturnToLaunch Validation ---");
        Console.WriteLine("[Audit] Executing RTL...");
        await drone.ReturnToLaunchAsync();

        // Wait for RTL (typically ascends, flies back, and lands)
        await Task.Delay(30000);
        
        var rtlTelemetry = _latestTelemetry;
        if (rtlTelemetry == null) throw new Exception("No telemetry received.");
        var rtlDistance = CalculateDistance(startLat, startLon, rtlTelemetry.Latitude, rtlTelemetry.Longitude);
        Console.WriteLine($"[Result] RTL End Position: Lat={rtlTelemetry.Latitude}, Lon={rtlTelemetry.Longitude}");
        Console.WriteLine($"[Result] Distance from Home: {rtlDistance:F2} meters");
        
        if (rtlDistance < 5.0) // 5m is safer for GPS drift
        {
            Console.WriteLine("✅ ReturnToLaunch Validation PASSED");
        }
        else
        {
            Console.WriteLine("❌ ReturnToLaunch Validation FAILED");
        }
        
        Console.WriteLine("\n[Audit] Validations Complete. Check results above.");
    }
}
