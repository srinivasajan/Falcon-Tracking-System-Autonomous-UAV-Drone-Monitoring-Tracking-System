using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Abstractions;

public interface IRuntimeHealthMonitor
{
    event EventHandler<RuntimeHealthSnapshot>? HealthChanged;
    Task<RuntimeHealthSnapshot> CheckAsync(RuntimeId runtimeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RuntimeHealthSnapshot>> CheckAllAsync(CancellationToken cancellationToken = default);
    RuntimeHealthSnapshot GetLatest(RuntimeId runtimeId);
    void Report(RuntimeHealthSnapshot snapshot);
}
