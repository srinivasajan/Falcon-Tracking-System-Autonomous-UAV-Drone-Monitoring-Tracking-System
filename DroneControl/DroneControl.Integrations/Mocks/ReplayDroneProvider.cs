using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Integrations.Mocks;

public sealed class ReplayDroneProvider : IDroneProvider
{
    private readonly ITelemetryRecorder _recorder;
    private readonly string _sessionId;
    private readonly ILogger<ReplayDroneProvider> _logger;
    private CancellationTokenSource? _replayCts;
    private Task? _replayTask;

    public ReplayDroneProvider(ITelemetryRecorder recorder, string sessionId, ILogger<ReplayDroneProvider> logger)
    {
        _recorder = recorder;
        _sessionId = sessionId;
        _logger = logger;
    }

    public string ProviderName => "Replay Provider";

    public DroneConnectionState ConnectionState { get; private set; } = DroneConnectionState.Disconnected;

    public TelemetrySnapshot CurrentTelemetry { get; private set; } = new TelemetrySnapshot(DateTimeOffset.UtcNow, false, 0, 0, 0, 0, 0, "Unknown");

    public event EventHandler<DroneConnectionState>? ConnectionStateChanged;
    public event EventHandler<TelemetrySnapshot>? TelemetryUpdated;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Replay drone connecting to session {SessionId}", _sessionId);
        
        ConnectionState = DroneConnectionState.Connected;
        ConnectionStateChanged?.Invoke(this, ConnectionState);

        var snapshots = await _recorder.LoadReplayAsync(_sessionId, cancellationToken);
        if (snapshots.Count == 0)
        {
            _logger.LogWarning("No telemetry found for session {SessionId}", _sessionId);
            return;
        }

        _replayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _replayTask = Task.Run(() => ReplayLoopAsync(snapshots, _replayCts.Token));
    }

    private async Task ReplayLoopAsync(IReadOnlyList<TelemetrySnapshot> snapshots, CancellationToken token)
    {
        _logger.LogInformation("Starting replay of {Count} snapshots.", snapshots.Count);

        for (int i = 0; i < snapshots.Count; i++)
        {
            if (token.IsCancellationRequested) break;

            var current = snapshots[i];
            CurrentTelemetry = current;
            TelemetryUpdated?.Invoke(this, current);

            if (i < snapshots.Count - 1)
            {
                var next = snapshots[i + 1];
                var delay = next.Timestamp - current.Timestamp;
                
                // Cap delay to avoid massive waits
                if (delay > TimeSpan.FromSeconds(5)) delay = TimeSpan.FromSeconds(1);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, token);
                }
            }
        }

        _logger.LogInformation("Replay finished.");
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Replay drone disconnecting.");
        if (_replayCts != null)
        {
            _replayCts.Cancel();
            try { await _replayTask!; } catch { }
        }

        ConnectionState = DroneConnectionState.Disconnected;
        ConnectionStateChanged?.Invoke(this, ConnectionState);
    }

    public Task ArmAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Replay Provider: Arm requested (ignored).");
        return Task.CompletedTask;
    }

    public Task DisarmAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Replay Provider: Disarm requested (ignored).");
        return Task.CompletedTask;
    }

    public Task TakeoffAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Replay Provider: Takeoff requested (ignored).");
        return Task.CompletedTask;
    }

    public Task LandAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Replay Provider: Land requested (ignored).");
        return Task.CompletedTask;
    }

    public Task GotoAsync(double latitude, double longitude, double altitudeMeters, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Replay Provider: Goto requested (ignored).");
        return Task.CompletedTask;
    }

    public Task ReturnToLaunchAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Replay Provider: RTL requested (ignored).");
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _replayCts?.Dispose();
    }
}
