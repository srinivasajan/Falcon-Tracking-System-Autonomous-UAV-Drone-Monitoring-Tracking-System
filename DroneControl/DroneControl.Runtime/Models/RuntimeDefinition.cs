namespace DroneControl.Runtime.Models;

public sealed record RuntimeDefinition(
    RuntimeId Id,
    string DisplayName,
    RuntimeKind Kind,
    string SupportedVersion,
    string RelativeInstallPath,
    string? ExecutableRelativePath,
    IReadOnlyList<string> DefaultArguments,
    bool RunsAsProcess,
    bool RequiredForPhaseOne,
    Uri ProjectUri);
