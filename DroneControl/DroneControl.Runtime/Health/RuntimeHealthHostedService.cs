using DroneControl.Runtime.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DroneControl.Runtime.Health;

public sealed class RuntimeHealthHostedService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    private readonly IRuntimeHealthMonitor _healthMonitor;
    private readonly RuntimeManagementOptions _options;
    private readonly ILogger<RuntimeHealthHostedService> _logger;

    public RuntimeHealthHostedService(
        IRuntimeHealthMonitor healthMonitor,
        IOptions<RuntimeManagementOptions> options,
        ILogger<RuntimeHealthHostedService> logger)
    {
        _healthMonitor = healthMonitor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _healthMonitor.CheckAllAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Runtime health monitoring failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.HealthCheckIntervalSeconds)), stoppingToken);
        }
    }
}
