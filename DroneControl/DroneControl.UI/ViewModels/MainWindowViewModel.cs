using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using DroneControl.Runtime.Abstractions;
using System.IO;
using DroneControl.Runtime.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DroneControl.UI.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IDroneProvider _droneProvider;
    private readonly ISimulatorProvider _simulatorProvider;
    private readonly IVisionProvider _visionProvider;
    private readonly ITrackingProvider _trackingProvider;
    private readonly ICommandPlanner _commandPlanner;
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
    private bool _isAutoTracking;
    private bool _isProvidersStarted;
    private bool _isDemoModeActive;

    // Latest tracker output — used by LockTarget to resolve correct TrackId
    private IReadOnlyList<TrackedObject> _lastTrackedObjects = Array.Empty<TrackedObject>();

    // Throttle autonomous navigation: don't issue GotoAsync every 500ms
    private DateTimeOffset _lastGotoAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan GotoThrottle = TimeSpan.FromSeconds(2);

    public MainWindowViewModel(
        IDroneProvider droneProvider,
        ISimulatorProvider simulatorProvider,
        IVisionProvider visionProvider,
        ITrackingProvider trackingProvider,
        ICommandPlanner commandPlanner,
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
        _commandPlanner = commandPlanner;
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
        StopProvidersCommand  = new RelayCommand(_ => _ = StopProvidersAsync());
        ArmCommand     = new RelayCommand(_ => _ = ExecuteDroneCommandAsync("Arm",     token => _droneProvider.ArmAsync(token)));
        DisarmCommand  = new RelayCommand(_ => _ = ExecuteDroneCommandAsync("Disarm",  token => _droneProvider.DisarmAsync(token)));
        TakeoffCommand = new RelayCommand(_ => _ = ExecuteDroneCommandAsync("Takeoff", token => _droneProvider.TakeoffAsync(token)));
        LandCommand    = new RelayCommand(_ => _ = ExecuteDroneCommandAsync("Land",    token => _droneProvider.LandAsync(token)));
        LockTargetCommand = new RelayCommand(parameter => LockTarget(parameter as DetectionResult),
                                             parameter => parameter is DetectionResult);

        RemoveWaypointCommand = new RelayCommand(_ => RemoveSelectedWaypoint(), _ => SelectedWaypoint != null);
        SaveMissionCommand    = new RelayCommand(_ => _ = SaveMissionAsync());
        LoadMissionCommand    = new RelayCommand(_ => _ = LoadMissionAsync());

        StartRecordingCommand = new RelayCommand(_ => _ = StartRecordingAsync(), _ => !IsRecording);
        StopRecordingCommand  = new RelayCommand(_ => _ = StopRecordingAsync(),  _ =>  IsRecording);

        StartAutoTrackingCommand = new RelayCommand(_ => StartAutoTracking(), _ => _targetLockService.CurrentLock != null && !IsAutoTracking);
        StopAutoTrackingCommand  = new RelayCommand(_ => StopAutoTracking(),  _ => IsAutoTracking);

        RefreshRuntimeValidationCommand = new RelayCommand(_ => _ = RefreshRuntimeValidationAsync());
        RunDemoCommand = new RelayCommand(_ => _ = RunDemoAsync(), _ => !IsDemoModeActive);

        AddEventFeedItem("Application started. Waiting for provider initialization.");

        _droneProvider.TelemetryUpdated          += OnTelemetryUpdated;
        _droneProvider.ConnectionStateChanged    += OnDroneConnectionStateChanged;
        _simulatorProvider.VirtualCameraFrameReady += OnFrameReady;
        _visionProvider.VisionEventEmitted       += OnVisionEventEmitted;
        _runtimeHealthMonitor.HealthChanged      += OnRuntimeHealthChanged;
        _targetLockService.TargetLockUpdated     += OnTargetLockUpdated;

        InitializeRuntimeValidationItems();
        _ = RefreshRuntimeValidationAsync();
    }

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<DetectionResult> Detections { get; } = [];
    public ObservableCollection<string> TelemetryLog { get; } = [];
    public ObservableCollection<EventFeedItem> EventFeed { get; } = [];
    public ObservableCollection<RuntimeValidationItemViewModel> RuntimeValidationItems { get; } = [];
    public ObservableCollection<Waypoint> MissionWaypoints { get; } = [];

    public ReplayViewModel ReplayViewModel { get; }

    // ── Commands ─────────────────────────────────────────────────────────────
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
    public ICommand StartAutoTrackingCommand { get; }
    public ICommand StopAutoTrackingCommand { get; }
    public ICommand RefreshRuntimeValidationCommand { get; }
    public ICommand RunDemoCommand { get; }

    // ── Properties ───────────────────────────────────────────────────────────
    public Waypoint? SelectedWaypoint
    {
        get => _selectedWaypoint;
        set
        {
            if (SetProperty(ref _selectedWaypoint, value))
                CommandManager.InvalidateRequerySuggested();
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

    public bool IsAutoTracking
    {
        get => _isAutoTracking;
        private set
        {
            if (SetProperty(ref _isAutoTracking, value))
            {
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(AutoTrackingStatusLabel));
                OnPropertyChanged(nameof(NavigationStatus));
                OnPropertyChanged(nameof(PursuitStateLabel));
                OnPropertyChanged(nameof(IsStep5Complete));
                if (value) AddEventFeedItem("Autonomous Pursuit Enabled");
            }
        }
    }

    public bool IsDemoModeActive
    {
        get => _isDemoModeActive;
        set => SetProperty(ref _isDemoModeActive, value);
    }

    public bool IsProvidersStarted
    {
        get => _isProvidersStarted;
        private set
        {
            SetProperty(ref _isProvidersStarted, value);
            OnPropertyChanged(nameof(VisionSystemStatus));
            OnPropertyChanged(nameof(IsStep1Complete));
        }
    }

    public string RecordingStatusLabel      => IsRecording     ? "RECORDING ACTIVE"      : "NOT RECORDING";
    public string AutoTrackingStatusLabel   => IsAutoTracking  ? "AUTO-TRACKING ACTIVE"  : "AUTO-TRACKING OFF";

    public string ConnectionStatus => _droneProvider.ConnectionState switch
    {
        DroneConnectionState.Connecting => $"Connecting via {_droneProvider.ProviderName}",
        DroneConnectionState.Connected  => $"Connected via {_droneProvider.ProviderName}",
        DroneConnectionState.Faulted    => $"Connection fault via {_droneProvider.ProviderName}",
        _ => "Disconnected"
    };

    public int    BatteryPercent => _telemetry.BatteryPercent;
    public string BatteryLabel   => $"{_telemetry.BatteryPercent}% battery";
    public string Latitude       => _telemetry.Latitude.ToString("F6");
    public string Longitude      => _telemetry.Longitude.ToString("F6");
    public string Speed          => $"{_telemetry.SpeedMetersPerSecond:F1} m/s";
    public string Altitude       => $"{_telemetry.AltitudeMeters:F1} m";
    public string FlightMode     => _telemetry.Mode;
    public Uri?   VideoUri       => _videoProvider.StreamUri;

    public string FrameLabel
    {
        get => _frameLabel;
        private set => SetProperty(ref _frameLabel, value);
    }

    public string TargetLockLabel =>
        _targetLockService.CurrentLock is null
            ? "No target selected"
            : $"TRK-{_targetLockService.CurrentLock.TrackId} – {_targetLockService.CurrentLock.Detection.ObjectType}";

    public string TargetConfidenceLabel =>
        _targetLockService.CurrentLock is null
            ? "Click a detection box to lock."
            : $"{_targetLockService.CurrentLock.Detection.Confidence:P0} confidence";

    public string TargetClassLabel => _targetLockService.CurrentLock?.Detection.ObjectType.ToString() ?? "None";
    public string PursuitStateLabel => IsAutoTracking ? "Active" : "Inactive";
    public string TrackerStatus => _lastTrackedObjects.Count > 0 ? "Tracking" : "Idle";
    public string NavigationStatus => IsAutoTracking ? "Autonomous" : "Manual";
    public string VisionSystemStatus => IsProvidersStarted ? "Running" : "Stopped";
    public string TargetDetectionStatus => Detections.Count > 0 ? "Active" : "Inactive";
    public string SafetyStatus => "Healthy";

    public bool HasCameraFeed => FrameLabel != "No frame";
    public bool HasTelemetry => TelemetryLog.Count > 0;
    
    // Guided Steps
    public bool IsStep1Complete => IsProvidersStarted;
    public bool IsStep2Complete => _droneProvider.ConnectionState == DroneConnectionState.Connected;
    public bool IsStep3Complete => Detections.Count > 0;
    public bool IsStep4Complete => _targetLockService.CurrentLock != null;
    public bool IsStep5Complete => IsAutoTracking;
    public bool IsStep6Complete => TelemetryLog.Count > 0;

    public string WaypointSummary => $"{_waypointCount} waypoint(s) in current mission";

    // ── Provider lifecycle ───────────────────────────────────────────────────
    private async Task StartProvidersAsync()
    {
        try
        {
            await _droneProvider.ConnectAsync();
            await _simulatorProvider.StartAsync();
            IsProvidersStarted = true;
            AddEventFeedItem("Providers Started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to start provider stack.");
            MessageBox.Show("DroneControl could not start the selected provider stack. Please check logs.",
                            "Provider error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StopProvidersAsync()
    {
        StopAutoTracking();
        await _simulatorProvider.StopAsync();
        await _droneProvider.DisconnectAsync();
        IsProvidersStarted = false;
    }

    // ── Telemetry handler ────────────────────────────────────────────────────
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
            OnPropertyChanged(nameof(HasTelemetry));
            OnPropertyChanged(nameof(IsStep6Complete));

            TelemetryLog.Insert(0, $"{telemetry.Timestamp:HH:mm:ss} BAT {telemetry.BatteryPercent,3}% ALT {telemetry.AltitudeMeters,5:F1} SPD {telemetry.SpeedMetersPerSecond,4:F1}");
            while (TelemetryLog.Count > 12)
                TelemetryLog.RemoveAt(TelemetryLog.Count - 1);
        });
    }

    private void OnDroneConnectionStateChanged(object? sender, DroneConnectionState state)
    {
        Application.Current.Dispatcher.Invoke(() => {
            OnPropertyChanged(nameof(ConnectionStatus));
            OnPropertyChanged(nameof(IsStep2Complete));
        });
    }

    // ── Frame / vision / tracking pipeline ───────────────────────────────────
    private void OnFrameReady(object? sender, CameraFrame frame)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            if (FrameLabel == "No frame")
            {
                OnPropertyChanged(nameof(HasCameraFeed));
            }
            FrameLabel = $"{frame.FrameId} – {frame.Width}×{frame.Height}";

            // 1. Run detection
            var detections = await _visionProvider.DetectAsync(frame);
            if (Detections.Count == 0 && detections.Count > 0)
            {
                AddEventFeedItem("Target Detected");
            }

            // 2. Run tracker — produces stable TrackIds
            var trackedObjects = await _trackingProvider.UpdateAsync(detections);

            // 3. Cache tracked objects so LockTarget() can resolve the correct TrackId
            _lastTrackedObjects = trackedObjects;

            // 4. Update the target lock position if one is active
            _targetLockService.UpdateFromTracks(trackedObjects);

            // 5. If auto-tracking is on, plan and execute navigation commands
            if (IsAutoTracking && _targetLockService.CurrentLock != null)
            {
                await ExecuteAutonomousNavigationAsync();
            }

            // 6. Persist tracking data if a session is active
            if (_telemetryRecorder.ActiveSessionId != null)
            {
                try
                {
                    await _trackingRepository.SaveTracksAsync(
                        _telemetryRecorder.ActiveSessionId, frame.Timestamp, trackedObjects);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save vision tracks to repository.");
                }
            }

            // 7. Refresh UI detection boxes
            Detections.Clear();
            foreach (var d in detections)
                Detections.Add(d);
                
            OnPropertyChanged(nameof(TargetDetectionStatus));
            OnPropertyChanged(nameof(TrackerStatus));
            OnPropertyChanged(nameof(IsStep3Complete));
        });
    }

    private async Task ExecuteAutonomousNavigationAsync()
    {
        // Throttle to avoid flooding the drone with GotoAsync calls
        if (DateTimeOffset.UtcNow - _lastGotoAt < GotoThrottle)
            return;

        var currentLock = _targetLockService.CurrentLock;
        if (currentLock == null) return;

        try
        {
            var commands = _commandPlanner.PlanCommands(null, null, _telemetry, currentLock);
            foreach (var cmd in commands)
            {
                if (cmd is DroneCommand.Goto gotoCmd)
                {
                    _logger.LogInformation(
                        "[AutoTrack] GotoAsync Lat={Lat:F6} Lon={Lon:F6} Alt={Alt:F1} for TrackId={TrackId}",
                        gotoCmd.Latitude, gotoCmd.Longitude, gotoCmd.Altitude,
                        currentLock.TrackId);

                    await _droneProvider.GotoAsync(gotoCmd.Latitude, gotoCmd.Longitude, gotoCmd.Altitude);
                    _lastGotoAt = DateTimeOffset.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Autonomous navigation command failed.");
        }
    }

    private void OnTargetLockUpdated(object? sender, TargetLockEvent e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _targetLock = e.CurrentLock;
            OnPropertyChanged(nameof(TargetLockLabel));
            OnPropertyChanged(nameof(TargetConfidenceLabel));
            OnPropertyChanged(nameof(TargetClassLabel));
            OnPropertyChanged(nameof(IsStep4Complete));
            // Refresh auto-tracking command availability when lock changes
            CommandManager.InvalidateRequerySuggested();
        });
    }

    private async void OnVisionEventEmitted(object? sender, VisionEvent visionEvent)
    {
        if (_telemetryRecorder.ActiveSessionId != null)
        {
            try
            {
                await _detectionRepository.SaveDetectionsAsync(
                    _telemetryRecorder.ActiveSessionId, visionEvent.Timestamp, visionEvent.Detections);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save vision detections to repository.");
            }
        }
    }

    // ── Target lock ──────────────────────────────────────────────────────────
    /// <summary>
    /// Called when the user clicks a detection bounding box in the UI.
    /// Resolves the real TrackId from the last tracker output before locking.
    /// </summary>
    private void LockTarget(DetectionResult? detection)
    {
        if (detection is null) return;

        // Find the TrackedObject whose Detection matches the clicked DetectionResult by ID
        var tracked = _lastTrackedObjects.FirstOrDefault(t => t.Detection.DetectionId == detection.DetectionId);

        if (tracked != null)
        {
            _targetLockService.RequestLock(tracked);
            AddEventFeedItem($"Target Locked ({tracked.Detection.ObjectType})");
            _logger.LogInformation(
                "[LockTarget] Locked TrackId={TrackId} ({ObjectType}) confidence={Conf:P0}",
                tracked.TrackId, tracked.Detection.ObjectType, tracked.Detection.Confidence);
        }
        else
        {
            // Tracker hasn't seen this detection yet (first frame edge case): fall back
            // to the first non-stale track of the same object type.
            var fallback = _lastTrackedObjects
                .Where(t => !t.IsStale && t.Detection.ObjectType == detection.ObjectType)
                .OrderBy(t => t.TrackId)
                .FirstOrDefault();

            if (fallback != null)
            {
                _targetLockService.RequestLock(fallback);
                _logger.LogInformation(
                    "[LockTarget] Fallback lock TrackId={TrackId} ({ObjectType})",
                    fallback.TrackId, fallback.Detection.ObjectType);
            }
            else
            {
                // Last resort: create a new single-use track
                _targetLockService.RequestLock(new TrackedObject(-1, detection, false));
                _logger.LogWarning("[LockTarget] No tracked object found; used ephemeral lock.");
            }
        }
    }

    // ── Autonomous tracking ──────────────────────────────────────────────────
    private void StartAutoTracking()
    {
        if (_targetLockService.CurrentLock == null)
        {
            MessageBox.Show("Lock a target first by clicking a detection box.",
                            "Auto Tracking", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        IsAutoTracking = true;
        _logger.LogInformation("[AutoTrack] Autonomous tracking STARTED for TrackId={TrackId}",
            _targetLockService.CurrentLock.TrackId);
    }

    private void StopAutoTracking()
    {
        if (!IsAutoTracking) return;
        IsAutoTracking = false;
        _logger.LogInformation("[AutoTrack] Autonomous tracking STOPPED.");
    }

    // ── Waypoints / mission ──────────────────────────────────────────────────
    public void AddWaypoint(double latitude, double longitude)
    {
        _waypointCount++;
        MissionWaypoints.Add(new Waypoint
        {
            Sequence  = _waypointCount,
            Latitude  = latitude,
            Longitude = longitude,
            Altitude  = 10.0
        });
        OnPropertyChanged(nameof(WaypointSummary));
    }

    private void RemoveSelectedWaypoint()
    {
        if (SelectedWaypoint != null)
        {
            MissionWaypoints.Remove(SelectedWaypoint);
            _waypointCount--;
            for (int i = 0; i < MissionWaypoints.Count; i++)
                MissionWaypoints[i].Sequence = i + 1;
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
                MissionWaypoints.ToList());
            await _missionStore.SaveAsync(plan);
            MessageBox.Show("Mission saved successfully to local SQLite database.",
                            "Save Mission", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MissionWaypoints.Add(wp);
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

    // ── Recording ────────────────────────────────────────────────────────────
    private async Task StartRecordingAsync()
    {
        try
        {
            var sessionId = $"Session {DateTime.Now:g}";
            string? videoPath = null;
            if (_videoProvider.StreamUri != null)
            {
                var logsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DroneControl", "logs", "videos");
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

    // ── Generic drone command executor ───────────────────────────────────────
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
            MessageBox.Show($"{commandName} failed. Please check logs and runtime status.",
                            "Drone command", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── Runtime validation ────────────────────────────────────────────────────
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
                UpdateRuntimeValidationItem(snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Runtime validation refresh failed.");
            MessageBox.Show("DroneControl could not refresh runtime validation status. Please check logs.",
                            "Runtime validation", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnRuntimeHealthChanged(object? sender, RuntimeHealthSnapshot snapshot)
    {
        Application.Current.Dispatcher.Invoke(() => UpdateRuntimeValidationItem(snapshot));
    }

    private void UpdateRuntimeValidationItem(RuntimeHealthSnapshot snapshot)
    {
        if (!_runtimeValidationItems.TryGetValue(snapshot.RuntimeId, out var item)) return;

        item.Installed = snapshot.IsInstalled ? "Yes" : "No";
        item.Version   = snapshot.Version.InstalledVersion ?? $"Supported {snapshot.Version.SupportedVersion}";
        item.Status    = snapshot.Diagnostics.Count == 0
            ? snapshot.Status.ToString()
            : $"{snapshot.Status}: {snapshot.Diagnostics[^1].Message}";
    }

    private static string GetEstimatedSize(RuntimeId runtimeId)
    {
        if (runtimeId == RuntimeId.Gazebo)     return "1.5-3.0 GB";
        if (runtimeId == RuntimeId.Px4)        return "200-800 MB";
        if (runtimeId == RuntimeId.Mavsdk)     return "20-100 MB";
        if (runtimeId == RuntimeId.Yolo)       return "300 MB-3 GB";
        if (runtimeId == RuntimeId.ByteTrack)  return "50-500 MB";
        if (runtimeId == RuntimeId.Ffmpeg)     return "80-200 MB";
        if (runtimeId == RuntimeId.Sqlite)     return "1-10 MB";
        return "Unknown";
    }

    private void AddEventFeedItem(string message)
    {
        Application.Current.Dispatcher.Invoke(() => {
            EventFeed.Insert(0, new EventFeedItem { Timestamp = DateTime.Now, Message = message });
            if (EventFeed.Count > 15) EventFeed.RemoveAt(EventFeed.Count - 1);
        });
    }

    private async Task RunDemoAsync()
    {
        IsDemoModeActive = true;
        try
        {
            // 1. Start Providers
            await StartProvidersAsync();
            await Task.Delay(2000);

            // 2. Wait for target to be visible
            while(Detections.Count == 0) await Task.Delay(500);
            
            // 3. Acquire Target Lock
            var firstTrack = _lastTrackedObjects.FirstOrDefault();
            if (firstTrack != null)
            {
                _targetLockService.RequestLock(firstTrack);
                AddEventFeedItem($"Demo: Target Locked");
            }
            await Task.Delay(1500);

            // 4. Start tracking pursuit
            StartAutoTracking();
            AddEventFeedItem("Demo: Autonomous Pursuit Active");
            
            await Task.Delay(10000); // Demo for 10s
            
            StopAutoTracking();
            await StopProvidersAsync();
            AddEventFeedItem("Demo Complete");
        }
        finally
        {
            IsDemoModeActive = false;
        }
    }
}

public class EventFeedItem
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
}
