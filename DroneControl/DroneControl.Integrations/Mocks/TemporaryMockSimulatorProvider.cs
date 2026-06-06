using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Integrations.Mocks;

/// <summary>
/// Temporary development mock only. Gazebo is the intended primary simulator provider.
/// </summary>
public sealed class TemporaryMockSimulatorProvider : ISimulatorProvider
{
    private readonly ILogger<TemporaryMockSimulatorProvider> _logger;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private int _tick;

    public TemporaryMockSimulatorProvider(ILogger<TemporaryMockSimulatorProvider> logger)
    {
        _logger = logger;
    }

    public string ProviderName => "Temporary Mock Simulator";
    public SimulatorState State { get; private set; } = SimulatorState.Stopped;

    public event EventHandler<SimulatorState>? StateChanged;
    public event EventHandler<CameraFrame>? VirtualCameraFrameReady;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_runTask is { IsCompleted: false })
        {
            return Task.CompletedTask;
        }

        SetState(SimulatorState.Starting);
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = RunAsync(_runCts.Token);
        SetState(SimulatorState.Running);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _runCts?.Cancel();
        if (_runTask is not null)
        {
            await _runTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        SetState(SimulatorState.Stopped);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _runCts?.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                _tick++;
                VirtualCameraFrameReady?.Invoke(this, new CameraFrame($"MOCK-{_tick:000000}", DateTimeOffset.Now, 1280, 720));
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Temporary mock simulator loop stopped.");
        }
    }

    private void SetState(SimulatorState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }
}
