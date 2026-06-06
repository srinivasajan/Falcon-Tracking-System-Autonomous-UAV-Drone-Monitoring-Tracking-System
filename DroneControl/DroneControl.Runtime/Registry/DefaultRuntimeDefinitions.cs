using DroneControl.Runtime.Models;

namespace DroneControl.Runtime.Registry;

public static class DefaultRuntimeDefinitions
{
    public static IReadOnlyList<RuntimeDefinition> All { get; } =
    [
        new(
            RuntimeId.Gazebo,
            "Gazebo",
            RuntimeKind.Simulator,
            SupportedVersion: "TBD",
            RelativeInstallPath: "gazebo",
            ExecutableRelativePath: "bin\\gazebo.exe",
            DefaultArguments: [],
            RunsAsProcess: true,
            RequiredForPhaseOne: true,
            ProjectUri: new Uri("https://gazebosim.org/")),
        new(
            RuntimeId.Px4,
            "PX4",
            RuntimeKind.FlightStack,
            SupportedVersion: "TBD",
            RelativeInstallPath: "px4",
            ExecutableRelativePath: "bin\\px4.exe",
            DefaultArguments: [],
            RunsAsProcess: true,
            RequiredForPhaseOne: true,
            ProjectUri: new Uri("https://px4.io/")),
        new(
            RuntimeId.Mavsdk,
            "MAVSDK",
            RuntimeKind.CommunicationSdk,
            SupportedVersion: "TBD",
            RelativeInstallPath: "mavsdk",
            ExecutableRelativePath: "bin\\mavsdk_server.exe",
            DefaultArguments: ["-p", "50051", "udp://:14540"],
            RunsAsProcess: true,
            RequiredForPhaseOne: true,
            ProjectUri: new Uri("https://mavsdk.mavlink.io/")),
        new(
            RuntimeId.Yolo,
            "Ultralytics YOLO",
            RuntimeKind.Vision,
            SupportedVersion: "TBD",
            RelativeInstallPath: "yolo",
            ExecutableRelativePath: null,
            DefaultArguments: [],
            RunsAsProcess: false,
            RequiredForPhaseOne: true,
            ProjectUri: new Uri("https://github.com/ultralytics/ultralytics")),
        new(
            RuntimeId.ByteTrack,
            "ByteTrack",
            RuntimeKind.Tracking,
            SupportedVersion: "TBD",
            RelativeInstallPath: "bytetrack",
            ExecutableRelativePath: null,
            DefaultArguments: [],
            RunsAsProcess: false,
            RequiredForPhaseOne: true,
            ProjectUri: new Uri("https://github.com/ifzhang/ByteTrack")),
        new(
            RuntimeId.Ffmpeg,
            "FFmpeg",
            RuntimeKind.Video,
            SupportedVersion: "TBD",
            RelativeInstallPath: "ffmpeg",
            ExecutableRelativePath: "bin\\ffmpeg.exe",
            DefaultArguments: ["-version"],
            RunsAsProcess: true,
            RequiredForPhaseOne: true,
            ProjectUri: new Uri("https://ffmpeg.org/")),
        new(
            RuntimeId.Sqlite,
            "SQLite",
            RuntimeKind.Storage,
            SupportedVersion: "3.x",
            RelativeInstallPath: "sqlite",
            ExecutableRelativePath: null,
            DefaultArguments: [],
            RunsAsProcess: false,
            RequiredForPhaseOne: true,
            ProjectUri: new Uri("https://sqlite.org/"))
    ];
}
