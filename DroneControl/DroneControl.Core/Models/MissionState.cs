namespace DroneControl.Core.Models;

public enum MissionState
{
    Idle,
    Starting,
    Executing,
    Paused,
    Completed,
    Failed,
    Cancelled
}
