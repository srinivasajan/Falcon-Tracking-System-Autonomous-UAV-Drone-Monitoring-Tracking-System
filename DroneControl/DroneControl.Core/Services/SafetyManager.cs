using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Core.Services;

public class SafetyManager : ISafetyManager
{
    private readonly ILogger<SafetyManager> _logger;
    private readonly ISafetyEventRepository _repo;
    private readonly string _sessionId;
    private readonly List<SafetyEvent> _activeWarnings = new();

    public SafetyLevel CurrentSafetyLevel { get; private set; } = SafetyLevel.Info;
    public IReadOnlyList<SafetyEvent> ActiveWarnings => _activeWarnings.AsReadOnly();

    public event EventHandler<SafetyEvent>? SafetyEventRaised;

    public SafetyManager(ILogger<SafetyManager> logger, ISafetyEventRepository repo, string sessionId)
    {
        _logger = logger;
        _repo = repo;
        _sessionId = sessionId;
    }

    public async Task EvaluateStateAsync(TelemetrySnapshot telemetry, IReadOnlyList<TrackedObject> tracks, IntegrationHealth visionHealth)
    {
        var prevLevel = CurrentSafetyLevel;
        CurrentSafetyLevel = SafetyLevel.Info;

        if (telemetry.BatteryPercent < 15)
        {
            await TriggerEventAsync(SafetyLevel.RTL, "Battery", "Battery critically low. Forcing RTL.");
        }
        else if (telemetry.BatteryPercent < 30)
        {
            await TriggerEventAsync(SafetyLevel.Warning, "Battery", "Battery low.");
        }

        if (!telemetry.IsConnected || DateTimeOffset.UtcNow - telemetry.Timestamp > TimeSpan.FromSeconds(3))
        {
            await TriggerEventAsync(SafetyLevel.RTL, "Telemetry", "Telemetry link lost or stale. Forcing RTL.");
        }

        if (telemetry.Mode.Contains("NoGPS", StringComparison.OrdinalIgnoreCase))
        {
            await TriggerEventAsync(SafetyLevel.RTL, "GPS", "GPS lock lost. Forcing RTL.");
        }

        if (!visionHealth.IsAvailable)
        {
            await TriggerEventAsync(SafetyLevel.Warning, "Vision", $"Vision subsystem health is unavailable: {visionHealth.Message}");
        }

        if (CurrentSafetyLevel == SafetyLevel.Info && _activeWarnings.Count == 0 && prevLevel != SafetyLevel.Info)
        {
            _logger.LogInformation("System returned to normal safety level.");
        }
    }

    public async Task ClearWarningAsync(Guid eventId)
    {
        var item = _activeWarnings.FirstOrDefault(x => x.Id == eventId);
        if (item != null)
        {
            _activeWarnings.Remove(item);
            CurrentSafetyLevel = _activeWarnings.Count > 0 ? _activeWarnings.Max(x => x.Level) : SafetyLevel.Info;
            await Task.CompletedTask;
        }
    }

    private async Task TriggerEventAsync(SafetyLevel level, string source, string description)
    {
        var evt = new SafetyEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, level, source, description);
        
        if (level > CurrentSafetyLevel)
            CurrentSafetyLevel = level;

        _activeWarnings.Add(evt);
        await _repo.SaveSafetyEventAsync(_sessionId, evt);
        SafetyEventRaised?.Invoke(this, evt);
        
        _logger.Log(level == SafetyLevel.RTL ? LogLevel.Critical : level == SafetyLevel.Abort ? LogLevel.Error : LogLevel.Warning,
            "Safety Event triggered: {Level} from {Source}: {Description}", level, source, description);
    }
}
