using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Runtime.Abstractions;
using System.IO;
using DroneControl.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace DroneControl.UI.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IDroneProvider _droneProvider;
    private readonly ISimulatorProvider _simulatorProvider;
    private readonly IVisionProvider _visionProvider;
    private readonly ITrackingProvider _trackingProvider;
    private readonly IVideoProvider _videoProvider;
    private readonly IVideoRecorderService _videoRecorderService;
    private readonly ITelemetryRecorder _telemetryRecorder;
    private readonly IDetectionRepository _detectionRepository;
    private readonly ITrackingRepository _trackingRepository;
    private readonly ITargetLockRepository _targetLockRepository;
    private readonly ITargetLockService _targetLockService;
    private readonly IRuntimeRegistry _runtimeRegistry;
    private readonly IRuntimeHealthMonitor _runtimeHealthMonitor;
    private readonly IMissionStore _missionStore;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly Dictionary<RuntimeId, RuntimeValidationItemViewModel> _runtimeValidationItems = [];
    private TelemetrySnapshot _telemetry;
    private TargetLock? _targetLock;
    private int _waypointCount;
    private string _frameLabel = "No frame";
    private Waypoint? _selectedWaypoint;
    private bool _isRecording;

    public MainWindowViewModel(
        IDroneProvider droneProvider,
        ISimulatorProvider simulatorProvider,
        IVisionProvider visionProvider,
        ITrackingProvider trackingProvider,
        IVideoProvider videoProvider,
        IVideoRecorderService videoRecorderService,
        ITelemetryRecorder telemetryRecorder,
        IDetectionRepository detectionRepository,
        ITrackingRepository trackingRepository,
        ITargetLockRepository targetLockRepository,
        ITargetLockService targetLockService,
        IRuntimeRegistry runtimeRegistry,
        IRuntimeHealthMonitor runtimeHealthMonitor,
        IMissionStore missionStore,
        ILogger<MainWindowViewModel> logger)
    {
        _droneProvider = droneProvider;
        _simulatorProvider = simulatorProvider;
        _visionProvider = visionProvider;
        _trackingProvider = trackingProvider;
        _videoProvider = videoProvider;
        _videoRecorderService = videoRecorderService;
        _telemetryRecorder = telemetryRecorder;
        _detectionRepository = detectionRepository;
        _trackingRepository = trackingRepository;
        _targetLockRepository = targetLockRepository;
        _targetLockService = targetLockService;
        _runtimeRegistry = runtimeRegistry;
        _runtimeHealthMonitor = runtimeHealthMonitor;
        _missionStore = missionStore;
        _logger = logger;
        _telemetry = droneProvider.CurrentTelemetry;
        
        ReplayViewModel = new ReplayViewModel(
            telemetryRecorder, 
            videoProvider,
            detectionRepository,
            trackingRepository,
            targetLockRepository);

        StartProvidersCommand = new RelayCommand(_ => _ = StartProvidersAsync());
        StopProvidersCommand = new RelayCommand(_ => _ = StopProvidersAsync());
        ArmCommand = new RelayCommand(_ => _ = ExecuteDroneCommandAsync("Arm", token => _droneProvider.ArmAsync(token)));
        DisarmCommand = new RelayCommand(_ => _ = ExecuteDroneCommandAsync("Disarm", token => _droneProvider.DisarmAsync(token)));
        TakeoffCommand = new RelayCommand(_ => _ = ExecuteDroneCommandAsync("Takeoff", token => _droneProvider.TakeoffAsync(token)));
        LandCommand = new RelayCommand(_ => _ = ExecuteDroneCommandAsync("Land", token => _droneProvider.LandAsync(token)));
        LockTargetCommand = new RelayCommand(parameter => LockTarget(parameter as DetectionResult), parameter => parameter is DetectionResult);
        
        // Old AddWaypointCommand is removed since Map clicks drive it now
        RemoveWaypointCommand = new RelayCommand(_ => RemoveSelectedWaypoint(), _ => SelectedWaypoint != null);
        SaveMissionCommand = new RelayCommand(_ => _ = SaveMissionAsync());
        LoadMissionCommand = new RelayCommand(_ => _ = LoadMissionAsync());
        
        StartRecordingCommand = new RelayCommand(_ => _ = StartRecordingAsync(), _ => !IsRecording);
        StopRecordingCommand = new RelayCommand(_ => _ = StopRecordingAsync(), _ => IsRecording);

        RefreshRuntimeValidationCommand = new RelayCommand(_ => _ = RefreshRuntimeValidationAsync());

        _droneProvider.TelemetryUpdated += OnTelemetryUpdated;
        _droneProvider.ConnectionStateChanged += OnDroneConnectionStateChanged;
        _simulatorProvider.VirtualCameraFrameReady += OnFrameReady;
        _visionProvider.VisionEventEmitted += OnVisionEventEmitted;
        _runtimeHealthMonitor.HealthChanged += OnRuntimeHealthChanged;
        _targetLockService.TargetLockUpdated += OnTargetLockUpdated;

        InitializeRuntimeValidationItems();
        _ = RefreshRuntimeValidationAsync();
    }

    public ObservableCollection<DetectionResult> Detections { get; } = [];
    public ObservableCollection<string> TelemetryLog { get; } = [];
    public ObservableCollection<RuntimeValidationItemViewModel> RuntimeValidationItems { get; } = [];
    public ObservableCollection<Waypoint> MissionWaypoints { get; } = [];

    public ReplayViewModel ReplayViewModel { get; }

    public Waypoint? SelectedWaypoint
    {
        get => _selectedWaypoint;
        set
        {
            if (SetProperty(ref _selectedWaypoint, value))
            {
                // Trigger command can-execute eval
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        set
        {
            if (SetProperty(ref _isRecording, value))
            {
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(RecordingStatusLabel));
            }
        }
    }

    public string RecordingStatusLabel => IsRecording ? "RECORDING ACTIVE" : "NOT RECORDING";

    public ICommand StartProvidersCommand { get; }
    public ICommand StopProvidersCommand { get; }
    public ICommand ArmCommand { get; }
    public ICommand DisarmCommand { get; }
    public ICommand TakeoffCommand { get; }
    public ICommand LandCommand { get; }
    public ICommand LockTargetCommand { get; }
    public ICommand RemoveWaypointCommand { get; }
    public ICommand SaveMissionCommand { get; }
    public ICommand LoadMissionCommand { get; }
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }
    public ICommand RefreshRuntimeValidationCommand { get; }

    public string ConnectionStatus => _droneProvider.ConnectionState switch
    {
        DroneConnectionState.Connecting => $"Connecting via {_droneProvider.ProviderName}",
        DroneConnectionState.Connected => $"Connected via {_droneProvider.ProviderName}",
        DroneConnectionState.Faulted => $"Connection fault via {_droneProvider.ProviderName}",
        _ => "Disconnected"
    };
    public int BatteryPercent => _telemetry.BatteryPercent;
    public string BatteryLabel => $"{_telemetry.BatteryPercent}% battery";
    public string Latitude => _telemetry.Latitude.ToString("F6");
    public string Longitude => _telemetry.Longitude.ToString("F6");
    public string Speed => $"{_telemetry.SpeedMetersPerSecond:F1} m/s";
    public string Altitude => $"{_telemetry.AltitudeMeters:F1} m";
    public string FlightMode => _telemetry.Mode;
    public Uri? VideoUri => _videoProvider.StreamUri;
    public string FrameLabel { get => _frameLabel; private set => SetProperty(ref _frameLabel, value); }
    public string TargetLockLabel => _targetLockService.CurrentLock is null ? "No target selected" : $"TRK-{_targetLockService.CurrentLock.TrackId} - {_targetLockService.CurrentLock.Detection.ObjectType}";
    public string TargetConfidenceLabel => _targetLockService.CurrentLock is null ? "Click a detection box to lock." : $"{_targetLockService.CurrentLock.Detection.Confidence:P0} confidence";
    public string WaypointSummary => $"{_waypointCount} waypoint(s) in current mission";

    private async Task StartProvidersAsync()
    {
        try
        {
            await _droneProvider.ConnectAsync();
            await _simulatorProvider.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to start provider stack.");
            MessageBox.Show("DroneControl could not start the selected provider stack. Please check logs.", "Provider error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StopProvidersAsync()
    {
        await _simulatorProvider.StopAsync();
        await _droneProvider.DisconnectAsync();
    }

    private void OnTelemetryUpdated(object? sender, TelemetrySnapshot telemetry)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            _telemetry = telemetry;
            await _telemetryRecorder.RecordAsync(telemetry);

            OnPropertyChanged(nameof(ConnectionStatus));
            OnPropertyChanged(nameof(BatteryPercent));
            OnPropertyChanged(nameof(BatteryLabel));
            OnPropertyChanged(nameof(Latitude));
            OnPropertyChanged(nameof(Longitude));
            OnPropertyChanged(nameof(Speed));
            OnPropertyChanged(nameof(Altitude));
            OnPropertyChanged(nameof(FlightMode));

            TelemetryLog.Insert(0, $"{telemetry.Timestamp:HH:mm:ss} BAT {telemetry.BatteryPercent,3}% ALT {telemetry.AltitudeMeters,5:F1} SPD {telemetry.SpeedMetersPerSecond,4:F1}");
            while (TelemetryLog.Count > 12)
            {
                TelemetryLog.RemoveAt(TelemetryLog.Count - 1);
            }
        });
    }

    private void OnDroneConnectionStateChanged(object? sender, DroneConnectionState state)
    {
        Application.Current.Dispatcher.Invoke(() => OnPropertyChanged(nameof(ConnectionStatus)));
    }

    private void OnFrameReady(object? sender, CameraFrame frame)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            FrameLabel = $"{frame.FrameId} - {frame.Width}x{frame.Height}";
            var detections = await _visionProvider.DetectAsync(frame);
            var trackedObjects = await _trackingProvider.UpdateAsync(detections);
            _targetLockService.UpdateFromTracks(trackedObjects);
            
            if (_telemetryRecorder.ActiveSessionId != null)
            {
                try
                {
                    await _trackingRepository.SaveTracksAsync(_telemetryRecorder.ActiveSessionId, frame.Timestamp, trackedObjects);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save vision tracks to repository.");
                }
            }

            Detections.Clear();
            foreach (var detection in detections)
            {
                Detections.Add(detection);
            }
        });
    }

    private void OnTargetLockUpdated(object? sender, TargetLockEvent e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(TargetLockLabel));
            OnPropertyChanged(nameof(TargetConfidenceLabel));
        });
    }

    private async void OnVisionEventEmitted(object? sender, VisionEvent visionEvent)
    {
        if (_telemetryRecorder.ActiveSessionId != null)
        {
            try
            {
                await _detectionRepository.SaveDetectionsAsync(_telemetryRecorder.ActiveSessionId, visionEvent.Timestamp, visionEvent.Detections);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save vision detections to repository.");
            }
        }
    }

    private void LockTarget(DetectionResult? detection)
    {
        if (detection is null)
        {
            return;
        }

        // Just create a dummy TrackedObject for the lock request, 
        // real system would look up the TrackId from the detection
        var dummyTrack = new TrackedObject(0, detection, false);
        _targetLockService.RequestLock(dummyTrack);
    }

    public void AddWaypoint(double latitude, double longitude)
    {
        _waypointCount++;
        MissionWaypoints.Add(new Waypoint 
        { 
            Sequence = _waypointCount, 
            Latitude = latitude, 
            Longitude = longitude,
            Altitude = 10.0 // Default 10 meters
        });
        OnPropertyChanged(nameof(WaypointSummary));
    }

    private void RemoveSelectedWaypoint()
    {
        if (SelectedWaypoint != null)
        {
            MissionWaypoints.Remove(SelectedWaypoint);
            _waypointCount--;
            // Re-sequence
            for (int i = 0; i < MissionWaypoints.Count; i++)
            {
                MissionWaypoints[i].Sequence = i + 1;
            }
            OnPropertyChanged(nameof(WaypointSummary));
        }
    }

    private async Task SaveMissionAsync()
    {
        try
        {
            var plan = new MissionPlan(
                Guid.NewGuid().ToString(),
                $"Mission {DateTime.Now:g}",
                DateTimeOffset.Now,
                MissionWaypoints.ToList()
            );

            await _missionStore.SaveAsync(plan);
            MessageBox.Show("Mission saved successfully to local SQLite database.", "Save Mission", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save mission.");
            MessageBox.Show("Failed to save mission. Check logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadMissionAsync()
    {
        try
        {
            var missions = await _missionStore.LoadAllAsync();
            var latest = missions.FirstOrDefault();
            
            if (latest is not null)
            {
                MissionWaypoints.Clear();
                foreach (var wp in latest.Waypoints.OrderBy(w => w.Sequence))
                {
                    MissionWaypoints.Add(wp);
                }
                _waypointCount = MissionWaypoints.Count;
                OnPropertyChanged(nameof(WaypointSummary));
                MessageBox.Show($"Loaded mission: {latest.Name}", "Load Mission", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No saved missions found.", "Load Mission", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mission.");
            MessageBox.Show("Failed to load mission. Check logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            var sessionId = $"Session {DateTime.Now:g}";
            string? videoPath = null;
            
            if (_videoProvider.StreamUri != null)
            {
                var logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroneControl", "logs", "videos");
                videoPath = Path.Combine(logsDir, $"{Guid.NewGuid()}.mp4");
                _videoRecorderService.StartRecording(_videoProvider.StreamUri, videoPath);
            }

            await _telemetryRecorder.StartRecordingAsync(sessionId, videoPath);
            IsRecording = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording.");
            MessageBox.Show("Failed to start recording. Check logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StopRecordingAsync()
    {
        try
        {
            await _telemetryRecorder.StopRecordingAsync();
            await _videoRecorderService.StopRecordingAsync();
            IsRecording = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop recording.");
            MessageBox.Show("Failed to stop recording. Check logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExecuteDroneCommandAsync(string commandName, Func<CancellationToken, Task> command)
    {
        try
        {
            _logger.LogInformation("Executing drone command {CommandName}.", commandName);
            await command(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drone command {CommandName} failed.", commandName);
            MessageBox.Show($"{commandName} failed. Please check logs and runtime status.", "Drone command", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void InitializeRuntimeValidationItems()
    {
        foreach (var definition in _runtimeRegistry.GetAll())
        {
            var item = new RuntimeValidationItemViewModel(definition.DisplayName, GetEstimatedSize(definition.Id));
            _runtimeValidationItems[definition.Id] = item;
            RuntimeValidationItems.Add(item);
        }
    }

    private async Task RefreshRuntimeValidationAsync()
    {
        try
        {
            var snapshots = await _runtimeHealthMonitor.CheckAllAsync();
            foreach (var snapshot in snapshots)
            {
                UpdateRuntimeValidationItem(snapshot);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Runtime validation refresh failed.");
            MessageBox.Show("DroneControl could not refresh runtime validation status. Please check logs.", "Runtime validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnRuntimeHealthChanged(object? sender, RuntimeHealthSnapshot snapshot)
    {
        Application.Current.Dispatcher.Invoke(() => UpdateRuntimeValidationItem(snapshot));
    }

    private void UpdateRuntimeValidationItem(RuntimeHealthSnapshot snapshot)
    {
        if (!_runtimeValidationItems.TryGetValue(snapshot.RuntimeId, out var item))
        {
            return;
        }

        item.Installed = snapshot.IsInstalled ? "Yes" : "No";
        item.Version = snapshot.Version.InstalledVersion ?? $"Supported {snapshot.Version.SupportedVersion}";
        item.Status = snapshot.Diagnostics.Count == 0
            ? snapshot.Status.ToString()
            : $"{snapshot.Status}: {snapshot.Diagnostics[^1].Message}";
    }

    private static string GetEstimatedSize(RuntimeId runtimeId)
    {
        if (runtimeId == RuntimeId.Gazebo)
        {
            return "1.5-3.0 GB";
        }

        if (runtimeId == RuntimeId.Px4)
        {
            return "200-800 MB";
        }

        if (runtimeId == RuntimeId.Mavsdk)
        {
            return "20-100 MB";
        }

        if (runtimeId == RuntimeId.Yolo)
        {
            return "300 MB-3 GB";
        }

        if (runtimeId == RuntimeId.ByteTrack)
        {
            return "50-500 MB";
        }

        if (runtimeId == RuntimeId.Ffmpeg)
        {
            return "80-200 MB";
        }

        if (runtimeId == RuntimeId.Sqlite)
        {
            return "1-10 MB";
        }

        return "Unknown";
    }
}
