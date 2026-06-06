using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface ITelemetryRecorder
{
    string? ActiveSessionId { get; }
    Task<string> StartRecordingAsync(string name, string? videoPath = null, CancellationToken cancellationToken = default);
    Task StopRecordingAsync(CancellationToken cancellationToken = default);
    Task RecordAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetrySession>> GetSessionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TelemetrySnapshot>> LoadReplayAsync(string sessionId, CancellationToken cancellationToken = default);
}
