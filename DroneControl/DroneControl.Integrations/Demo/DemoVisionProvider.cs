using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

namespace DroneControl.Integrations.Demo;

public class DemoVisionProvider : IVisionProvider, ISimulatorProvider
{
    public string ProviderName => "Demo Vision";
    public IReadOnlySet<DetectedObjectType> SupportedObjectTypes { get; } = new HashSet<DetectedObjectType> { DetectedObjectType.Person };

    public event EventHandler<VisionEvent>? VisionEventEmitted;
    public event EventHandler<SimulatorState>? StateChanged;
    public event EventHandler<CameraFrame>? VirtualCameraFrameReady;

    public SimulatorState State { get; private set; } = SimulatorState.Stopped;

    private CancellationTokenSource? _runCts;
    private int _tick;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        State = SimulatorState.Running;
        StateChanged?.Invoke(this, State);
        
        _runCts = new CancellationTokenSource();
        _ = RunLoopAsync(_runCts.Token);
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _runCts?.Cancel();
        State = SimulatorState.Stopped;
        StateChanged?.Invoke(this, State);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _runCts?.Cancel();
        return ValueTask.CompletedTask;
    }

    public Task<IReadOnlyList<DetectionResult>> DetectAsync(CameraFrame frame, CancellationToken cancellationToken = default)
    {
        // Generate a fake moving person detection based on the current tick
        var x = 200 + Math.Sin(_tick / 20.0) * 150;
        var y = 150 + Math.Cos(_tick / 25.0) * 100;
        
        var result = new DetectionResult(
            DetectionId: $"demo-person",
            ObjectType: DetectedObjectType.Person,
            Confidence: 0.95,
            X: x,
            Y: y,
            Width: 80,
            Height: 180);

        return Task.FromResult<IReadOnlyList<DetectionResult>>([result]);
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
        while (await timer.WaitForNextTickAsync(token))
        {
            _tick++;
            
            // Emit fake camera frame to satisfy UI requirements
            var frame = new CameraFrame($"DEMO-{_tick:000000}", DateTimeOffset.Now, 1280, 720);
            VirtualCameraFrameReady?.Invoke(this, frame);

            // Emit the vision event directly as well to simulate real hardware emitting events
            var detections = await DetectAsync(frame, token);
            VisionEventEmitted?.Invoke(this, new VisionEvent(DateTimeOffset.Now, detections));
        }
    }
}
