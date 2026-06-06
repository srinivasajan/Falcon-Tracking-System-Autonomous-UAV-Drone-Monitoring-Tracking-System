using DroneControl.Core.Models;

namespace DroneControl.Integrations.MAVSDK;

public sealed class MavsdkProviderOptions
{
    public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.ManagedLocal;
    public string GrpcAddress { get; set; } = "http://127.0.0.1:50051";
    public int GrpcPort { get; set; } = 50051;
    public string SystemAddress { get; set; } = "udp://:14540";
    public int ConnectionTimeoutSeconds { get; set; } = 30;
}
