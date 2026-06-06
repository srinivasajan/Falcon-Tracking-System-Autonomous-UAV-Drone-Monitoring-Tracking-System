namespace DroneControl.Runtime.Models;

public sealed record RuntimeDiagnostic(
    RuntimeDiagnosticSeverity Severity,
    string Code,
    string Message,
    DateTimeOffset Timestamp);
