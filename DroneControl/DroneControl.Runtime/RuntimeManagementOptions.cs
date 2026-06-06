namespace DroneControl.Runtime;

public sealed class RuntimeManagementOptions
{
    public string? RuntimeRoot { get; set; }
    public int HealthCheckIntervalSeconds { get; set; } = 30;
}
