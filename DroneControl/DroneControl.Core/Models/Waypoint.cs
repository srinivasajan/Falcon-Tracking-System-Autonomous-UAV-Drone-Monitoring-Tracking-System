namespace DroneControl.Core.Models;

public class Waypoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Sequence { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    
    // Future expansion fields
    public double Speed { get; set; }
    public double HoldTime { get; set; }
    public string CameraAction { get; set; } = string.Empty;
    public string GimbalAction { get; set; } = string.Empty;
    public string WaitForObjectType { get; set; } = string.Empty;
    public string OnDetectAction { get; set; } = string.Empty;
}
