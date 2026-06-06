using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Abstractions;

public interface IManagedRuntimeFactory
{
    IManagedRuntime Create(RuntimeId runtimeId);
    IReadOnlyList<IManagedRuntime> CreateAll();
}
