using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Health;
using DroneControl.Runtime.Managed;
using DroneControl.Runtime.Packaging;
using DroneControl.Runtime.Processes;
using DroneControl.Runtime.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace DroneControl.Runtime;

public static class DependencyInjection
{
    public static IServiceCollection AddDroneControlRuntimeManagement(this IServiceCollection services)
    {
        services.AddOptions<RuntimeManagementOptions>();
        services.AddSingleton<IRuntimeRegistry, RuntimeRegistry>();
        services.AddSingleton<IRuntimePathResolver, RuntimePathResolver>();
        services.AddSingleton<IRuntimeVersionStore, FileRuntimeVersionStore>();
        services.AddSingleton<IRuntimeProcessManager, RuntimeProcessManager>();
        services.AddSingleton<IRuntimePackageInstaller, FileSystemRuntimePackageInstaller>();
        services.AddSingleton<IRuntimeHealthMonitor, RuntimeHealthMonitor>();
        services.AddSingleton<IManagedRuntimeFactory, ManagedRuntimeFactory>();
        services.AddHostedService<RuntimeHealthHostedService>();
        return services;
    }
}
