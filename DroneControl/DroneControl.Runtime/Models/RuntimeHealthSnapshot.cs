namespace DroneControl.Runtime.Models;

public sealed record RuntimeHealthSnapshot(
    RuntimeId RuntimeId,
    RuntimeStatus Status,
    bool IsInstalled,
    RuntimeVersionInfo Version,
    int? ProcessId,
    int? LastExitCode,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RuntimeDiagnostic> Diagnostics);
