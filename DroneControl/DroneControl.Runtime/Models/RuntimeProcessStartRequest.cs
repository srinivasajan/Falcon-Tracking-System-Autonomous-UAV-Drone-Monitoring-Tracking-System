namespace DroneControl.Runtime.Models;

public sealed record RuntimeProcessStartRequest(
    RuntimeId RuntimeId,
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> EnvironmentVariables);
