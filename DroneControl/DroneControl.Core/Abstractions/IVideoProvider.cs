using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface IVideoProvider
{
    Uri? StreamUri { get; }
    Uri? ReplayStreamUri { get; }
    Task StartRecordingAsync(string sessionId, CancellationToken cancellationToken = default);
    Task StopRecordingAsync(CancellationToken cancellationToken = default);
    
    void LoadReplay(Uri replayUri);
    void UnloadReplay();
}
