using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace TestRunner;

public static class VisionMissionValidation
{
    public static async Task RunAsync()
    {
        Console.WriteLine("Starting Vision-Driven Mission Actions Validation...");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<StoragePaths>();
        services.AddDroneControlStorage();
        
        var provider = services.BuildServiceProvider();

        var paths = provider.GetRequiredService<StoragePaths>();
        paths.EnsureCreated();

        var missionStore = provider.GetRequiredService<IMissionStore>();

        var waypoint1 = new Waypoint
        {
            Sequence = 1,
            Latitude = 47.6062,
            Longitude = -122.3321,
            Altitude = 20,
            WaitForObjectType = "Person",
            OnDetectAction = "LockAndFollow"
        };

        var missionPlan = new MissionPlan(
            Id: Guid.NewGuid().ToString(),
            Name: "Vision Validation Mission",
            CreatedAt: DateTimeOffset.Now,
            Waypoints: new List<Waypoint> { waypoint1 }
        );

        Console.WriteLine("Saving mission with vision-driven actions...");
        await missionStore.SaveAsync(missionPlan);

        Console.WriteLine("Reloading mission...");
        var loadedMissions = await missionStore.LoadAllAsync();
        
        var loaded = loadedMissions.FirstOrDefault(m => m.Id == missionPlan.Id);
        if (loaded == null)
        {
            Console.WriteLine("RESULT: ❌ Failed (Mission not found)");
            return;
        }

        var loadedWp = loaded.Waypoints.FirstOrDefault();
        if (loadedWp == null)
        {
            Console.WriteLine("RESULT: ❌ Failed (Waypoint missing)");
            return;
        }

        Console.WriteLine($"Loaded WaitForObjectType: '{loadedWp.WaitForObjectType}'");
        Console.WriteLine($"Loaded OnDetectAction: '{loadedWp.OnDetectAction}'");

        if (loadedWp.WaitForObjectType == "Person" && loadedWp.OnDetectAction == "LockAndFollow")
        {
            Console.WriteLine("RESULT: ✅ Proven");
        }
        else
        {
            Console.WriteLine("RESULT: ❌ Failed (Fields did not persist)");
        }
    }
}
