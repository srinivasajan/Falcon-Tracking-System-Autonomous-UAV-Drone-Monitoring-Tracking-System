namespace DroneControl.Core.Models;

public sealed record TelemetrySnapshot(
    DateTimeOffset Timestamp,
    bool IsConnected,
    int BatteryPercent,
    double Latitude,
    double Longitude,
    double SpeedMetersPerSecond,
    double AltitudeMeters,
    string Mode);
