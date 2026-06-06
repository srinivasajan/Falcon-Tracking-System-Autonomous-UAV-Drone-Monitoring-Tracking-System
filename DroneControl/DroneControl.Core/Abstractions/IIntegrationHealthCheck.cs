using DroneControl.Core.Models;

namespace DroneControl.Core.Abstractions;

public interface IIntegrationHealthCheck
{
    string IntegrationName { get; }
    Task<IntegrationHealth> CheckAsync(CancellationToken cancellationToken = default);
}
