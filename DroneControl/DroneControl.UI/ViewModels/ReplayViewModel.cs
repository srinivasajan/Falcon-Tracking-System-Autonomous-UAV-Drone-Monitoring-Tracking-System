using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

namespace DroneControl.UI.ViewModels;

public sealed class ReplayViewModel : ObservableObject, IDisposable
{
    private readonly ITelemetryRecorder _telemetryRecorder;
    private readonly IVideoProvider _videoProvider;
    private readonly IDetectionRepository _detectionRepository;
    private readonly ITrackingRepository _trackingRepository;
    private readonly ITargetLockRepository _targetLockRepository;
    private TelemetrySession? _selectedSession;
    private TelemetrySnapshot? _currentTelemetry;

    public event EventHandler? PlayRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler<TimeSpan>? SeekRequested;

    public ReplayViewModel(
        ITelemetryRecorder telemetryRecorder, 
        IVideoProvider videoProvider,
        IDetectionRepository detectionRepository,
        ITrackingRepository trackingRepository,
        ITargetLockRepository targetLockRepository)
    {
        _telemetryRecorder = telemetryRecorder;
        _videoProvider = videoProvider;
        _detectionRepository = detectionRepository;
        _trackingRepository = trackingRepository;
        _targetLockRepository = targetLockRepository;
        Engine = new TelemetryReplayEngine();
        Engine.TelemetryEmitted += OnTelemetryEmitted;

        LoadSessionsCommand = new RelayCommand(_ => _ = LoadSessionsAsync());
        LoadSelectedSessionCommand = new RelayCommand(_ => _ = LoadSelectedSessionAsync(), _ => SelectedSession != null);
        PlayCommand = new RelayCommand(_ => { Engine.Play(); PlayRequested?.Invoke(this, EventArgs.Empty); }, _ => !Engine.IsPlaying && Engine.TotalDuration > TimeSpan.Zero);
        PauseCommand = new RelayCommand(_ => { Engine.Pause(); PauseRequested?.Invoke(this, EventArgs.Empty); }, _ => Engine.IsPlaying);

        // Populate initially
        _ = LoadSessionsAsync();
    }

    public ObservableCollection<TelemetrySession> Sessions { get; } = new();

    public TelemetryReplayEngine Engine { get; }

    public TelemetrySession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ICommand LoadSessionsCommand { get; }
    public ICommand LoadSelectedSessionCommand { get; }
    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }

    // Display bindings
    public int BatteryPercent => _currentTelemetry?.BatteryPercent ?? 0;
    public string BatteryLabel => $"{BatteryPercent}% battery";
    public string Latitude => _currentTelemetry?.Latitude.ToString("F6") ?? "0.000000";
    public string Longitude => _currentTelemetry?.Longitude.ToString("F6") ?? "0.000000";
    public string Speed => $"{_currentTelemetry?.SpeedMetersPerSecond ?? 0:F1} m/s";
    public string Altitude => $"{_currentTelemetry?.AltitudeMeters ?? 0:F1} m";
    public string FlightMode => _currentTelemetry?.Mode ?? "Unknown";
    
    public IReadOnlyList<DetectionResult> Detections => Engine.CurrentDetections;
    public IReadOnlyList<TrackedObject> Tracks => Engine.CurrentTracks;
    
    public string TargetLockLabel => Engine.CurrentTargetLock is null ? "No target locked" : $"TRK-{Engine.CurrentTargetLock.TrackId} - {Engine.CurrentTargetLock.Detection.ObjectType}";
    public string TargetConfidenceLabel => Engine.CurrentTargetLock is null ? string.Empty : $"{Engine.CurrentTargetLock.Detection.Confidence:P0} confidence";

    public Uri? ReplayVideoUri => _videoProvider.ReplayStreamUri;

    // Playback Speeds
    public ObservableCollection<double> AvailableSpeeds { get; } = new() { 0.5, 1.0, 2.0, 4.0 };

    public double SelectedPlaybackSpeed
    {
        get => Engine.PlaybackSpeed;
        set
        {
            if (Engine.PlaybackSpeed != value)
            {
                Engine.PlaybackSpeed = value;
                OnPropertyChanged();
            }
        }
    }

    // Time scrub slider
    public double CurrentTimeSeconds
    {
        get => Engine.CurrentPosition.TotalSeconds;
        set
        {
            // Only update engine if different to prevent infinite property change loop
            if (Math.Abs(Engine.CurrentPosition.TotalSeconds - value) > 0.001)
            {
                var ts = TimeSpan.FromSeconds(value);
                Engine.Seek(ts);
                SeekRequested?.Invoke(this, ts);
            }
        }
    }

    public double TotalTimeSeconds => Engine.TotalDuration.TotalSeconds;
    
    public string TimelineLabel => $"{Engine.CurrentPosition:mm\\:ss} / {Engine.TotalDuration:mm\\:ss}";

    public async Task LoadSessionsAsync()
    {
        try
        {
            var sessions = await _telemetryRecorder.GetSessionsAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                Sessions.Clear();
                foreach (var s in sessions)
                {
                    Sessions.Add(s);
                }
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load telemetry sessions.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadSelectedSessionAsync()
    {
        if (SelectedSession == null) return;

        try
        {
            var snapshots = await _telemetryRecorder.LoadReplayAsync(SelectedSession.Id);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(SelectedSession.VideoPath) && System.IO.File.Exists(SelectedSession.VideoPath))
                {
                    _videoProvider.LoadReplay(new Uri(SelectedSession.VideoPath));
                }
                else
                {
                    _videoProvider.UnloadReplay();
                }
                OnPropertyChanged(nameof(ReplayVideoUri));
                CommandManager.InvalidateRequerySuggested();
            });
            
            var detections = await _detectionRepository.LoadAllDetectionsForSessionAsync(SelectedSession.Id);
            var tracks = await _trackingRepository.LoadAllTracksForSessionAsync(SelectedSession.Id);
            var locks = await _targetLockRepository.LoadAllTargetLocksForSessionAsync(SelectedSession.Id);
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                Engine.LoadVisionData(detections, tracks, locks);
                Engine.LoadSnapshots(snapshots);
                OnPropertyChanged(nameof(TotalTimeSeconds));
                OnPropertyChanged(nameof(TimelineLabel));
                CommandManager.InvalidateRequerySuggested();
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to load session data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnTelemetryEmitted(object? sender, TelemetrySnapshot snapshot)
    {
        _currentTelemetry = snapshot;
        OnPropertyChanged(nameof(BatteryPercent));
        OnPropertyChanged(nameof(BatteryLabel));
        OnPropertyChanged(nameof(Latitude));
        OnPropertyChanged(nameof(Longitude));
        OnPropertyChanged(nameof(Speed));
        OnPropertyChanged(nameof(Altitude));
        OnPropertyChanged(nameof(FlightMode));
        
        OnPropertyChanged(nameof(CurrentTimeSeconds));
        OnPropertyChanged(nameof(TimelineLabel));
        
        OnPropertyChanged(nameof(Detections));
        OnPropertyChanged(nameof(Tracks));
        OnPropertyChanged(nameof(TargetLockLabel));
        OnPropertyChanged(nameof(TargetConfidenceLabel));
        
        CommandManager.InvalidateRequerySuggested();
    }

    public void Dispose()
    {
        Engine.TelemetryEmitted -= OnTelemetryEmitted;
        Engine.Dispose();
    }
}
