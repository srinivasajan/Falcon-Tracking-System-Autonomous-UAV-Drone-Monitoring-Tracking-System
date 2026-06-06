namespace DroneControl.Storage;

public sealed class StoragePaths
{
    public string RootPath { get; }
    public string MissionsPath => Path.Combine(RootPath, "missions");
    public string ReplaysPath => Path.Combine(RootPath, "replays");
    public string MediaPath => Path.Combine(RootPath, "media");

    public StoragePaths()
    {
        RootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DroneControl");
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(MissionsPath);
        Directory.CreateDirectory(ReplaysPath);
        Directory.CreateDirectory(MediaPath);
    }
}
