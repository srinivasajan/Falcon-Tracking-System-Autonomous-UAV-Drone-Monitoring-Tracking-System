using DroneControl.Core.Models;

namespace DroneControl.Integrations.Contracts;

public static class IntegrationCatalog
{
    public static IReadOnlyList<ExternalComponent> Components { get; } =
    [
        new("PX4", "Open-source autopilot and simulation flight stack", new Uri("https://px4.io/"), "Bundled or managed as a versioned runtime package"),
        new("MAVSDK", "High-level MAVLink SDK for vehicle communication", new Uri("https://mavsdk.mavlink.io/"), "Bundled native libraries with .NET interop boundary"),
        new("MAVLink", "Drone communication protocol ecosystem", new Uri("https://mavlink.io/"), "Used through MAVSDK and compatible providers"),
        new("Gazebo", "Primary simulator runtime", new Uri("https://gazebosim.org/"), "Bundled or managed by installer/runtime manager"),
        new("Ultralytics YOLO", "Object detection provider", new Uri("https://github.com/ultralytics/ultralytics"), "Bundled model/runtime environment managed internally"),
        new("ByteTrack", "Primary multi-object tracking provider", new Uri("https://github.com/ifzhang/ByteTrack"), "Bundled tracking runtime managed internally"),
        new("FFmpeg", "Video ingest, encoding, snapshots, and replay processing", new Uri("https://ffmpeg.org/"), "Bundled binary under installer-controlled tools directory"),
        new("OpenStreetMap", "Map data source", new Uri("https://www.openstreetmap.org/"), "Used through tile provider/cache with attribution")
    ];
}
