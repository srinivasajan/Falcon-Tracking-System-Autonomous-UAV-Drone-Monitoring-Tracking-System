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
    public static async Task Main(string[] args)
    {
        // Phase 1 completion sprint — run first
        Console.WriteLine("=== PHASE 1 PIPELINE VALIDATION ===");
        bool phase1Pass = await DroneControl.TestRunner.Phase1PipelineValidation.RunAsync();

        Console.WriteLine("\n=== PHASE 7, 8, 9 VALIDATIONS ===");
        await UnifiedReplayValidation.RunAsync();
        Console.WriteLine("\n-----------------------------------\n");
        await MissionExecutionValidation.RunAsync();

        Console.WriteLine($"\nPhase 1 Overall: {(phase1Pass ? "PASS ✅" : "FAIL ❌")}");
    }
}
