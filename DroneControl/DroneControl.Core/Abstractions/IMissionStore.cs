using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface IMissionStore
{
    Task SaveAsync(MissionPlan mission, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MissionPlan>> LoadAllAsync(CancellationToken cancellationToken = default);
}
