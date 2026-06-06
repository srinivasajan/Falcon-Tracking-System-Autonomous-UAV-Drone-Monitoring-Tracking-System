using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.Extensions.Logging;
using DroneControl.Core.Abstractions;

namespace DroneControl.Integrations.FFmpeg;

/// <summary>
/// Headless background service to record RTSP/UDP streams using FFMpegCore.
/// Does not interfere with the UI video player.
/// </summary>
public sealed class VideoRecorderService : IVideoRecorderService
{
    private readonly ILogger<VideoRecorderService> _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _recordingTask;

    public VideoRecorderService(ILogger<VideoRecorderService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Starts recording a video stream to the specified output path.
    /// </summary>
    public void StartRecording(Uri streamUri, string outputPath)
    {
        if (_recordingTask != null && !_recordingTask.IsCompleted)
        {
            _logger.LogWarning("A recording session is already active.");
            return;
        }

        _logger.LogInformation("Starting video recording from {StreamUri} to {OutputPath}", streamUri, outputPath);

        var ffmpegBinDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroneControl", "runtimes", "ffmpeg", "bin");
        if (Directory.Exists(ffmpegBinDir))
        {
            GlobalFFOptions.Configure(new FFOptions { BinaryFolder = ffmpegBinDir });
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        _recordingTask = Task.Run(async () =>
        {
            try
            {
                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Uses stream copy (no re-encoding) for ultra-low CPU usage.
                await FFMpegArguments
                    .FromUrlInput(streamUri)
                    .OutputToFile(outputPath, overwrite: true, options => options
                        .WithCustomArgument("-c copy"))
                    .CancellableThrough(token)
                    .ProcessAsynchronously();
                    
                _logger.LogInformation("Video recording finished normally.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Video recording was stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FFMpegCore video recording failed.");
            }
        }, token);
    }

    /// <summary>
    /// Stops the active recording gracefully.
    /// </summary>
    public async Task StopRecordingAsync()
    {
        if (_cancellationTokenSource != null)
        {
            _logger.LogInformation("Stopping video recording...");
            await _cancellationTokenSource.CancelAsync();
            
            if (_recordingTask != null)
            {
                try
                {
                    await _recordingTask;
                }
                catch { }
            }
            
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            _recordingTask = null;
        }
    }
}
