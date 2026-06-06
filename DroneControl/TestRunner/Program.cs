using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Integrations;
using DroneControl.Integrations.MAVSDK;
using DroneControl.Integrations.PX4;
using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TestRunner;

public static class Program
{
    public static async Task Main()
    {
        Console.WriteLine("=== PHASE 7, 8, 9 VALIDATIONS ===");
        await UnifiedReplayValidation.RunAsync();
        Console.WriteLine("\n-----------------------------------\n");
        await MissionExecutionValidation.RunAsync();
    }
}
