using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Abstractions;

public interface IRuntimeRegistry
{
    IReadOnlyList<RuntimeDefinition> GetAll();
    RuntimeDefinition GetRequired(RuntimeId runtimeId);
    bool TryGet(RuntimeId runtimeId, out RuntimeDefinition definition);
}
