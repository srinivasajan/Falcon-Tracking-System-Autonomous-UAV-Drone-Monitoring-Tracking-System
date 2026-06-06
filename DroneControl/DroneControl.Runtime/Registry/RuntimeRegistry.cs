using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Registry;

public sealed class RuntimeRegistry : IRuntimeRegistry
{
    private readonly Dictionary<RuntimeId, RuntimeDefinition> _definitions;

    public RuntimeRegistry()
    {
        _definitions = DefaultRuntimeDefinitions.All.ToDictionary(definition => definition.Id);
    }

    public IReadOnlyList<RuntimeDefinition> GetAll() => _definitions.Values.ToArray();

    public RuntimeDefinition GetRequired(RuntimeId runtimeId)
    {
        return TryGet(runtimeId, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Runtime '{runtimeId}' is not registered.");
    }

    public bool TryGet(RuntimeId runtimeId, out RuntimeDefinition definition)
    {
        return _definitions.TryGetValue(runtimeId, out definition!);
    }
}
