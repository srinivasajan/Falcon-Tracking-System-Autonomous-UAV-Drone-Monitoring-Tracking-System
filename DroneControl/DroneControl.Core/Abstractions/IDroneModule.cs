using Microsoft.Extensions.DependencyInjection;

namespace DroneControl.Core.Abstractions;

public interface IDroneModule
{
    string Name { get; }
    void Register(IServiceCollection services);
}
