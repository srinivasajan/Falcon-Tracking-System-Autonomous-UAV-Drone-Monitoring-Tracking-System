namespace DroneControl.Runtime.Models;

public readonly record struct RuntimeId(string Value)
{
    public override string ToString() => Value;

    public static RuntimeId Gazebo { get; } = new("gazebo");
    public static RuntimeId Px4 { get; } = new("px4");
    public static RuntimeId Mavsdk { get; } = new("mavsdk");
    public static RuntimeId Yolo { get; } = new("yolo");
    public static RuntimeId ByteTrack { get; } = new("bytetrack");
    public static RuntimeId Ffmpeg { get; } = new("ffmpeg");
    public static RuntimeId Sqlite { get; } = new("sqlite");
}
