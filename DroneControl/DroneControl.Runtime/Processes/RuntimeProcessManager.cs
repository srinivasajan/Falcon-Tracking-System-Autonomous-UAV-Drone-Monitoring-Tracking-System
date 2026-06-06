using System.Diagnostics;
using DroneControl.Runtime.Abstractions;
using DroneControl.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.Runtime.Processes;

public sealed class RuntimeProcessManager : IRuntimeProcessManager
{
    private readonly ILogger<RuntimeProcessManager> _logger;
    private readonly Dictionary<RuntimeId, Process> _processes = [];
    private readonly object _sync = new();

    public RuntimeProcessManager(ILogger<RuntimeProcessManager> logger)
    {
        _logger = logger;
    }

    public event EventHandler<RuntimeOutput>? OutputReceived;
    public event EventHandler<RuntimeExit>? ProcessExited;

    public bool IsRunning(RuntimeId runtimeId)
    {
        lock (_sync)
        {
            return _processes.TryGetValue(runtimeId, out var process) && !process.HasExited;
        }
    }

    public Task<RuntimeProcessHandle> StartAsync(RuntimeProcessStartRequest request, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_processes.TryGetValue(request.RuntimeId, out var existing) && !existing.HasExited)
            {
                return Task.FromResult(new RuntimeProcessHandle(request.RuntimeId, existing.Id, DateTimeOffset.Now));
            }

            var process = CreateProcess(request);
            process.OutputDataReceived += (_, args) => PublishOutput(request.RuntimeId, isError: false, args.Data);
            process.ErrorDataReceived += (_, args) => PublishOutput(request.RuntimeId, isError: true, args.Data);
            process.Exited += (_, _) => PublishExit(request.RuntimeId, process);

            _logger.LogInformation("Starting runtime {RuntimeId}: {ExecutablePath} {Arguments}", request.RuntimeId, request.ExecutablePath, string.Join(" ", request.Arguments));

            if (!process.Start())
            {
                throw new InvalidOperationException($"Runtime process '{request.RuntimeId}' failed to start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _processes[request.RuntimeId] = process;

            return Task.FromResult(new RuntimeProcessHandle(request.RuntimeId, process.Id, DateTimeOffset.Now));
        }
    }

    public async Task StopAsync(RuntimeId runtimeId, TimeSpan gracefulTimeout, CancellationToken cancellationToken = default)
    {
        Process? process;
        lock (_sync)
        {
            if (!_processes.TryGetValue(runtimeId, out process) || process.HasExited)
            {
                return;
            }
        }

        _logger.LogInformation("Stopping runtime {RuntimeId}.", runtimeId);
        process.CloseMainWindow();

        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(gracefulTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Runtime {RuntimeId} did not stop within {Timeout}; killing process.", runtimeId, gracefulTimeout);
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
        }
    }

    public async Task RestartAsync(RuntimeProcessStartRequest request, TimeSpan gracefulTimeout, CancellationToken cancellationToken = default)
    {
        await StopAsync(request.RuntimeId, gracefulTimeout, cancellationToken);
        await StartAsync(request, cancellationToken);
    }

    private static Process CreateProcess(RuntimeProcessStartRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in request.EnvironmentVariables)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        return new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
    }

    private void PublishOutput(RuntimeId runtimeId, bool isError, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _logger.Log(isError ? LogLevel.Warning : LogLevel.Information, "{RuntimeId}: {Output}", runtimeId, text);
        OutputReceived?.Invoke(this, new RuntimeOutput(runtimeId, isError, text, DateTimeOffset.Now));
    }

    private void PublishExit(RuntimeId runtimeId, Process process)
    {
        lock (_sync)
        {
            _processes.Remove(runtimeId);
        }

        _logger.LogInformation("Runtime {RuntimeId} exited with code {ExitCode}.", runtimeId, process.ExitCode);
        ProcessExited?.Invoke(this, new RuntimeExit(runtimeId, process.ExitCode, DateTimeOffset.Now));
        process.Dispose();
    }
}
