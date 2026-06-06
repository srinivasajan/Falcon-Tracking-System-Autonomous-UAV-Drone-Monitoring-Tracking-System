using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Runtime.Managed;

public sealed class ManagedRuntimeFactory : IManagedRuntimeFactory
{
    private readonly IRuntimeRegistry _runtimeRegistry;
    private readonly IRuntimePathResolver _pathResolver;
    private readonly IRuntimePackageInstaller _packageInstaller;
    private readonly IRuntimeProcessManager _processManager;
    private readonly IRuntimeHealthMonitor _healthMonitor;
    private readonly ILoggerFactory _loggerFactory;

    public ManagedRuntimeFactory(
        IRuntimeRegistry runtimeRegistry,
        IRuntimePathResolver pathResolver,
        IRuntimePackageInstaller packageInstaller,
        IRuntimeProcessManager processManager,
        IRuntimeHealthMonitor healthMonitor,
        ILoggerFactory loggerFactory)
    {
        _runtimeRegistry = runtimeRegistry;
        _pathResolver = pathResolver;
        _packageInstaller = packageInstaller;
        _processManager = processManager;
        _healthMonitor = healthMonitor;
        _loggerFactory = loggerFactory;
    }

    public IManagedRuntime Create(RuntimeId runtimeId)
    {
        return Create(_runtimeRegistry.GetRequired(runtimeId));
    }

    public IReadOnlyList<IManagedRuntime> CreateAll()
    {
        return _runtimeRegistry.GetAll().Select(Create).ToArray();
    }

    private IManagedRuntime Create(RuntimeDefinition definition)
    {
        return new GenericManagedRuntime(
            definition,
            _pathResolver,
            _packageInstaller,
            _processManager,
            _healthMonitor,
            _loggerFactory.CreateLogger<GenericManagedRuntime>());
    }
}
