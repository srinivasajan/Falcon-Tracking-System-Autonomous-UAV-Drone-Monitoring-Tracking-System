using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Runtime.Health;

public sealed class RuntimeHealthMonitor : IRuntimeHealthMonitor
{
    private readonly IRuntimeRegistry _runtimeRegistry;
    private readonly IRuntimePathResolver _pathResolver;
    private readonly IRuntimeVersionStore _versionStore;
    private readonly IRuntimeProcessManager _processManager;
    private readonly ILogger<RuntimeHealthMonitor> _logger;
    private readonly Dictionary<RuntimeId, RuntimeHealthSnapshot> _latest = [];

    public RuntimeHealthMonitor(
        IRuntimeRegistry runtimeRegistry,
        IRuntimePathResolver pathResolver,
        IRuntimeVersionStore versionStore,
        IRuntimeProcessManager processManager,
        ILogger<RuntimeHealthMonitor> logger)
    {
        _runtimeRegistry = runtimeRegistry;
        _pathResolver = pathResolver;
        _versionStore = versionStore;
        _processManager = processManager;
        _logger = logger;

        _processManager.ProcessExited += (_, exit) => ReportProcessExit(exit);
    }

    public event EventHandler<RuntimeHealthSnapshot>? HealthChanged;

    public async Task<RuntimeHealthSnapshot> CheckAsync(RuntimeId runtimeId, CancellationToken cancellationToken = default)
    {
        var definition = _runtimeRegistry.GetRequired(runtimeId);
        var diagnostics = new List<RuntimeDiagnostic>();
        var installDirectory = _pathResolver.GetInstallDirectory(definition);
        var executablePath = _pathResolver.GetExecutablePath(definition);
        var isInstalled = Directory.Exists(installDirectory);

        if (!isInstalled)
        {
            diagnostics.Add(Error("runtime_missing", $"{definition.DisplayName} is not installed at '{installDirectory}'."));
        }

        if (definition.RunsAsProcess && executablePath is not null && !File.Exists(executablePath))
        {
            diagnostics.Add(Error("executable_missing", $"{definition.DisplayName} executable is missing at '{executablePath}'."));
            isInstalled = false;
        }

        // Specific FFmpeg validation
        if (runtimeId == RuntimeId.Ffmpeg)
        {
            var ffprobePath = Path.Combine(installDirectory, "bin", "ffprobe.exe");
            if (!File.Exists(ffprobePath))
            {
                diagnostics.Add(Error("ffprobe_missing", $"ffprobe.exe is missing at '{ffprobePath}'."));
                isInstalled = false;
            }
        }
        
        // Specific YOLO validation
        if (runtimeId == RuntimeId.Yolo)
        {
            var modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroneControl", "models", "yolov8n.onnx");
            if (!File.Exists(modelPath))
            {
                diagnostics.Add(Error("model_missing", $"yolov8n.onnx model is missing at '{modelPath}'."));
                isInstalled = false;
            }
            else
            {
                isInstalled = true; // In-process model exists
            }
        }
        
        // Specific ByteTrack (IoU Tracking) validation
        if (runtimeId == RuntimeId.ByteTrack)
        {
            isInstalled = true; // In-process logic always installed
        }
        
        // Specific SQLite validation
        if (runtimeId == RuntimeId.Sqlite)
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroneControl", "data", "dronecontrol.db");
            if (!File.Exists(dbPath))
            {
                diagnostics.Add(Warning("db_missing", $"dronecontrol.db does not exist yet at '{dbPath}'."));
            }
            isInstalled = true; // SQLite engine always installed via EF/Data.Sqlite
        }

        var version = await _versionStore.GetVersionAsync(definition, cancellationToken);
        if (version.InstalledVersion is null && isInstalled)
        {
            diagnostics.Add(Warning("version_unknown", $"{definition.DisplayName} is installed but no DroneControl runtime version manifest was found."));
        }

        var status = RuntimeStatus.Unknown;
        if (definition.RunsAsProcess)
        {
            status = _processManager.IsRunning(runtimeId)
                ? RuntimeStatus.Running
                : isInstalled ? RuntimeStatus.Installed : RuntimeStatus.Missing;
        }
        else
        {
            // For in-process dependencies
            status = isInstalled ? RuntimeStatus.Running : RuntimeStatus.Missing;
        }

        var snapshot = new RuntimeHealthSnapshot(
            runtimeId,
            status,
            isInstalled,
            version,
            ProcessId: null,
            LastExitCode: null,
            DateTimeOffset.Now,
            diagnostics);

        Report(snapshot);
        return snapshot;
    }

    public async Task<IReadOnlyList<RuntimeHealthSnapshot>> CheckAllAsync(CancellationToken cancellationToken = default)
    {
        var snapshots = new List<RuntimeHealthSnapshot>();
        foreach (var definition in _runtimeRegistry.GetAll())
        {
            snapshots.Add(await CheckAsync(definition.Id, cancellationToken));
        }

        return snapshots;
    }

    public RuntimeHealthSnapshot GetLatest(RuntimeId runtimeId)
    {
        return _latest.TryGetValue(runtimeId, out var snapshot)
            ? snapshot
            : new RuntimeHealthSnapshot(
                runtimeId,
                RuntimeStatus.Unknown,
                IsInstalled: false,
                new RuntimeVersionInfo(null, "TBD", false, null),
                ProcessId: null,
                LastExitCode: null,
                DateTimeOffset.Now,
                [Warning("not_checked", "Runtime health has not been checked yet.")]);
    }

    public void Report(RuntimeHealthSnapshot snapshot)
    {
        _latest[snapshot.RuntimeId] = snapshot;
        _logger.LogInformation("Runtime {RuntimeId} health: {Status}.", snapshot.RuntimeId, snapshot.Status);
        HealthChanged?.Invoke(this, snapshot);
    }

    private void ReportProcessExit(RuntimeExit exit)
    {
        var current = GetLatest(exit.RuntimeId);
        var severity = exit.ExitCode == 0 ? RuntimeDiagnosticSeverity.Info : RuntimeDiagnosticSeverity.Error;
        var diagnostic = new RuntimeDiagnostic(
            severity,
            "process_exited",
            $"Runtime process exited with code {exit.ExitCode}.",
            DateTimeOffset.Now);

        Report(current with
        {
            Status = exit.ExitCode == 0 ? RuntimeStatus.Stopped : RuntimeStatus.Failed,
            LastExitCode = exit.ExitCode,
            UpdatedAt = DateTimeOffset.Now,
            Diagnostics = current.Diagnostics.Concat([diagnostic]).ToArray()
        });
    }

    private static RuntimeDiagnostic Error(string code, string message)
    {
        return new RuntimeDiagnostic(RuntimeDiagnosticSeverity.Error, code, message, DateTimeOffset.Now);
    }

    private static RuntimeDiagnostic Warning(string code, string message)
    {
        return new RuntimeDiagnostic(RuntimeDiagnosticSeverity.Warning, code, message, DateTimeOffset.Now);
    }
}
