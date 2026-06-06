# Git Changeset Report

**Date:** June 5, 2026
**Target Architecture:** Windows C# `.NET 8.0` DroneControl

## Git Status Verification
The local repository was verified using `git status`. Currently, the codebase is completely untracked because no initial commit has been made (`No commits yet`). Therefore, `git diff` yields an empty response. 

However, below is the explicit list of all modified files tracking the structural changes made to completely decouple `TemporaryMockDroneProvider` and tightly couple `MavsdkProvider` into the live DI sequence.

## Modified Files List & Code Diff Explanations

### 1. `DroneControl.Integrations/DependencyInjection.cs`
- **Removed:** `services.AddSingleton<IDroneProvider, TemporaryMockDroneProvider>();`
- **Removed:** Duplicate using statements.
- **Added:** `services.AddPx4MavsdkProviders();` wrapper usage validated.
- **Proof of Change:**
```csharp
    public static IServiceCollection AddDevelopmentMockProviders(this IServiceCollection services)
    {
        // Removed TemporaryMockDroneProvider from Mock Providers
        services.AddSingleton<ISimulatorProvider, TemporaryMockSimulatorProvider>();
        ...
```

### 2. `TestRunner/MissionExecutionValidation.cs`
- **Removed:** Direct dependency on the mock.
- **Added:** Direct registration of `AddPx4MavsdkProviders()`.
- **Proof of Change:**
```csharp
        // Replaced Mock Provider with MAVSDK
        services.AddPx4MavsdkProviders();
```

### 3. `TestRunner/Program.cs`
- **Removed:** Unused mock initialization sequence.
- **Added:** Integration of `http://localhost:50051` as the `GrpcAddress` config override to target the WSL MAVSDK endpoint.
- **Added:** Strong typing matches for `TelemetrySnapshot` (e.g. `BatteryPercent`, `AltitudeMeters`).
- **Proof of Change:**
```csharp
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"DroneControl:MAVSDK:ConnectionMode", "ExternalEndpoint"},
                {"DroneControl:MAVSDK:GrpcAddress", "http://localhost:50051"},
                {"DroneControl:MAVSDK:SystemAddress", "udp://:14540"}
            })
```

### 4. `DroneControl.UI/appsettings.json`
- **Modified:** Changed default `Mode` from `Mock` to `Simulation`.
- **Proof of Change:**
```json
{
  "DroneControl": {
    "Mode": "Simulation",
...
```

### 5. `DroneControl.Integrations/MAVSDK/MavsdkProvider.cs`
- **Added:** Comprehensive tracking of exact `ActionResult` strings for audit trails.
- **Proof of Change:**
```csharp
        else
        {
            _logger.LogInformation("MAVSDK action {ActionName} succeeded. ActionResult: {Result} ({ResultText})", actionName, result.Result, result.ResultStr);
        }
```

## Readiness
The codebase is now clean of DI mock resolutions for the primary `IDroneProvider` sequence, and ready for an initial commit.
