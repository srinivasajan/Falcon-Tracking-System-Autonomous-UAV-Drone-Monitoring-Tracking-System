using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Runtime.Managed;

public sealed class GenericManagedRuntime : IManagedRuntime
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);
    private readonly IRuntimePathResolver _pathResolver;
    private readonly IRuntimePackageInstaller _packageInstaller;
    private readonly IRuntimeProcessManager _processManager;
    private readonly IRuntimeHealthMonitor _healthMonitor;
    private readonly ILogger<GenericManagedRuntime> _logger;

    public GenericManagedRuntime(
        RuntimeDefinition definition,
        IRuntimePathResolver pathResolver,
        IRuntimePackageInstaller packageInstaller,
        IRuntimeProcessManager processManager,
        IRuntimeHealthMonitor healthMonitor,
        ILogger<GenericManagedRuntime> logger)
    {
        Definition = definition;
        _pathResolver = pathResolver;
        _packageInstaller = packageInstaller;
        _processManager = processManager;
        _healthMonitor = healthMonitor;
        _logger = logger;
    }

    public RuntimeDefinition Definition { get; }

    public RuntimeHealthSnapshot CurrentHealth => _healthMonitor.GetLatest(Definition.Id);

    public Task<RuntimeHealthSnapshot> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return _healthMonitor.CheckAsync(Definition.Id, cancellationToken);
    }

    public async Task InstallAsync(RuntimePackageManifest manifest, CancellationToken cancellationToken = default)
    {
        await ReportAsync(RuntimeStatus.Starting, "install_started", $"Installing {Definition.DisplayName}.", cancellationToken);
        await _packageInstaller.InstallAsync(manifest, cancellationToken);
        await CheckHealthAsync(cancellationToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var health = await CheckHealthAsync(cancellationToken);
        if (!health.IsInstalled)
        {
            await ReportAsync(RuntimeStatus.Missing, "runtime_missing", $"{Definition.DisplayName} is not installed.", cancellationToken);
            return;
        }

        if (!Definition.RunsAsProcess)
        {
            await ReportAsync(RuntimeStatus.Installed, "runtime_library_ready", $"{Definition.DisplayName} is managed as a library/runtime asset.", cancellationToken);
            return;
        }

        var executablePath = _pathResolver.GetExecutablePath(Definition);
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            await ReportAsync(RuntimeStatus.Failed, "executable_missing", $"{Definition.DisplayName} executable is missing.", cancellationToken);
            return;
        }

        await ReportAsync(RuntimeStatus.Starting, "process_starting", $"Starting {Definition.DisplayName}.", cancellationToken);

        var request = new RuntimeProcessStartRequest(
            Definition.Id,
            executablePath,
            Definition.DefaultArguments,
            _pathResolver.GetInstallDirectory(Definition),
            new Dictionary<string, string>());

        var handle = await _processManager.StartAsync(request, cancellationToken);
        var version = CurrentHealth.Version;
        _healthMonitor.Report(CurrentHealth with
        {
            Status = RuntimeStatus.Running,
            ProcessId = handle.ProcessId,
            Version = version,
            UpdatedAt = DateTimeOffset.Now
        });
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!Definition.RunsAsProcess)
        {
            await ReportAsync(RuntimeStatus.Stopped, "runtime_library_stopped", $"{Definition.DisplayName} has no external process to stop.", cancellationToken);
            return;
        }

        await ReportAsync(RuntimeStatus.Stopping, "process_stopping", $"Stopping {Definition.DisplayName}.", cancellationToken);
        await _processManager.StopAsync(Definition.Id, StopTimeout, cancellationToken);
        await ReportAsync(RuntimeStatus.Stopped, "process_stopped", $"{Definition.DisplayName} stopped.", cancellationToken);
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        await StartAsync(cancellationToken);
    }

    private async Task ReportAsync(RuntimeStatus status, string code, string message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{RuntimeName}: {Message}", Definition.DisplayName, message);
        var version = await _healthMonitor.CheckAsync(Definition.Id, cancellationToken);
        var diagnostic = new RuntimeDiagnostic(
            status == RuntimeStatus.Failed || status == RuntimeStatus.Missing ? RuntimeDiagnosticSeverity.Error : RuntimeDiagnosticSeverity.Info,
            code,
            message,
            DateTimeOffset.Now);

        _healthMonitor.Report(version with
        {
            Status = status,
            UpdatedAt = DateTimeOffset.Now,
            Diagnostics = version.Diagnostics.Concat([diagnostic]).ToArray()
        });
    }
}
