using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Integrations.FFmpeg;

public sealed class FfmpegVideoProvider : IVideoProvider
{
    private readonly IVideoRecorderService _recorder;
    private readonly ILogger<FfmpegVideoProvider> _logger;
    private readonly string _storageDir;
    private string? _currentSessionId;
    
    // Default mock stream for now (RTSP from a standard local test camera or simulator)
    public Uri? StreamUri { get; } = new Uri("rtsp://localhost:8554/stream");
    
    public Uri? ReplayStreamUri { get; private set; }

    public FfmpegVideoProvider(IVideoRecorderService recorder, ILogger<FfmpegVideoProvider> logger)
    {
        _recorder = recorder;
        _logger = logger;
        _storageDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroneControl", "video");
        Directory.CreateDirectory(_storageDir);
    }

    public Task StartRecordingAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _currentSessionId = sessionId;
        var outputPath = Path.Combine(_storageDir, $"{sessionId}.mp4");
        _recorder.StartRecording(StreamUri!, outputPath);
        _logger.LogInformation("Started recording video for session {SessionId} to {Path}", sessionId, outputPath);
        return Task.CompletedTask;
    }

    public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        await _recorder.StopRecordingAsync();
        _currentSessionId = null;
        _logger.LogInformation("Stopped recording video.");
    }

    public void LoadReplay(Uri replayUri)
    {
        ReplayStreamUri = replayUri;
        _logger.LogInformation("Loaded video replay from {Uri}", replayUri);
    }

    public void UnloadReplay()
    {
        ReplayStreamUri = null;
        _logger.LogInformation("Unloaded video replay.");
    }
}
