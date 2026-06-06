using DroneControl.Core.Models;

namespace DroneControl.UI.ViewModels;

public class TrackedObjectViewModel : ObservableObject
{
    private int _trackId;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private string _label = string.Empty;
    private bool _isLocked;
    private double _confidence;

    public int TrackId { get => _trackId; set => SetProperty(ref _trackId, value); }
    public double X { get => _x; set => SetProperty(ref _x, value); }
    public double Y { get => _y; set => SetProperty(ref _y, value); }
    public double Width { get => _width; set => SetProperty(ref _width, value); }
    public double Height { get => _height; set => SetProperty(ref _height, value); }
    public string Label { get => _label; set => SetProperty(ref _label, value); }
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }
    public double Confidence { get => _confidence; set => SetProperty(ref _confidence, value); }
    
    // Original detection used for locking
    public DetectionResult? OriginalDetection { get; set; }
}
