using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DroneControl.Core.Models;

namespace DroneControl.UI.ViewModels;

public sealed class TelemetryReplayEngine : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _timer;
    private IReadOnlyList<TelemetrySnapshot> _snapshots = Array.Empty<TelemetrySnapshot>();
    private DateTimeOffset _sessionStartTime;
    private bool _isPlaying;
    private double _playbackSpeed = 1.0;
    private TimeSpan _currentPosition;
    private TimeSpan _totalDuration;
    private int _currentIndex;
    private DateTimeOffset _lastTickTime;
    
    private IReadOnlyDictionary<DateTimeOffset, IReadOnlyList<DetectionResult>> _detections = new Dictionary<DateTimeOffset, IReadOnlyList<DetectionResult>>();
    private IReadOnlyDictionary<DateTimeOffset, IReadOnlyList<TrackedObject>> _tracks = new Dictionary<DateTimeOffset, IReadOnlyList<TrackedObject>>();
    private IReadOnlyDictionary<DateTimeOffset, TargetLockEvent> _targetLocks = new Dictionary<DateTimeOffset, TargetLockEvent>();
    
    private IReadOnlyList<DetectionResult> _currentDetections = Array.Empty<DetectionResult>();
    private IReadOnlyList<TrackedObject> _currentTracks = Array.Empty<TrackedObject>();
    private TargetLock? _currentTargetLock;

    public TelemetryReplayEngine()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // 20 Hz tick
        };
        _timer.Tick += OnTick;
    }

    public event EventHandler<TelemetrySnapshot>? TelemetryEmitted;

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                if (_isPlaying)
                {
                    _lastTickTime = DateTimeOffset.UtcNow;
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                }
            }
        }
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => SetProperty(ref _playbackSpeed, value);
    }

    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        set
        {
            if (SetProperty(ref _currentPosition, value))
            {
                UpdateCurrentIndex();
                EmitCurrentSnapshot();
            }
        }
    }

    public TimeSpan TotalDuration
    {
        get => _totalDuration;
        private set => SetProperty(ref _totalDuration, value);
    }
    
    public IReadOnlyList<DetectionResult> CurrentDetections
    {
        get => _currentDetections;
        private set => SetProperty(ref _currentDetections, value);
    }
    
    public IReadOnlyList<TrackedObject> CurrentTracks
    {
        get => _currentTracks;
        private set => SetProperty(ref _currentTracks, value);
    }
    
    public TargetLock? CurrentTargetLock
    {
        get => _currentTargetLock;
        private set => SetProperty(ref _currentTargetLock, value);
    }

    public void LoadSnapshots(IReadOnlyList<TelemetrySnapshot> snapshots)
    {
        IsPlaying = false;
        _snapshots = snapshots;

        if (_snapshots.Count > 0)
        {
            _sessionStartTime = _snapshots[0].Timestamp;
            TotalDuration = _snapshots[^1].Timestamp - _sessionStartTime;
            _currentIndex = 0;
            CurrentPosition = TimeSpan.Zero;
        }
        else
        {
            TotalDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            _currentIndex = -1;
        }
    }
    
    public void LoadVisionData(
        IReadOnlyDictionary<DateTimeOffset, IReadOnlyList<DetectionResult>> detections,
        IReadOnlyDictionary<DateTimeOffset, IReadOnlyList<TrackedObject>> tracks,
        IReadOnlyDictionary<DateTimeOffset, TargetLockEvent> targetLocks)
    {
        _detections = detections;
        _tracks = tracks;
        _targetLocks = targetLocks;
    }

    public void Play()
    {
        if (_snapshots.Count == 0) return;
        
        if (CurrentPosition >= TotalDuration)
        {
            CurrentPosition = TimeSpan.Zero;
        }
        IsPlaying = true;
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void Seek(TimeSpan position)
    {
        if (position < TimeSpan.Zero) position = TimeSpan.Zero;
        if (position > TotalDuration) position = TotalDuration;
        
        CurrentPosition = position;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_isPlaying || _snapshots.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        var delta = now - _lastTickTime;
        _lastTickTime = now;

        var advancedDelta = TimeSpan.FromMilliseconds(delta.TotalMilliseconds * PlaybackSpeed);
        var nextPosition = CurrentPosition + advancedDelta;

        if (nextPosition >= TotalDuration)
        {
            nextPosition = TotalDuration;
            IsPlaying = false;
        }

        CurrentPosition = nextPosition;
    }

    private void UpdateCurrentIndex()
    {
        if (_snapshots.Count == 0) return;

        var targetTime = _sessionStartTime + CurrentPosition;

        // Binary search could be used, but since we mostly tick forward sequentially, 
        // a simple scan from current index or binary search is fine.
        int index = _currentIndex;
        
        // If we jumped backwards
        if (index >= _snapshots.Count || (index >= 0 && _snapshots[index].Timestamp > targetTime))
        {
            index = 0;
        }

        while (index < _snapshots.Count - 1 && _snapshots[index + 1].Timestamp <= targetTime)
        {
            index++;
        }

        _currentIndex = index;
    }

    private void EmitCurrentSnapshot()
    {
        if (_currentIndex >= 0 && _currentIndex < _snapshots.Count)
        {
            var telemetry = _snapshots[_currentIndex];
            TelemetryEmitted?.Invoke(this, telemetry);
            
            // Find closest vision data
            var targetTime = telemetry.Timestamp;
            
            var closestDetectionTime = FindClosestTime(_detections.Keys, targetTime);
            CurrentDetections = closestDetectionTime != default ? _detections[closestDetectionTime] : Array.Empty<DetectionResult>();
            
            var closestTrackTime = FindClosestTime(_tracks.Keys, targetTime);
            CurrentTracks = closestTrackTime != default ? _tracks[closestTrackTime] : Array.Empty<TrackedObject>();
            
            // For target locks, we need the most recent state (not just closest)
            var recentLockTime = FindMostRecentTime(_targetLocks.Keys, targetTime);
            CurrentTargetLock = recentLockTime != default ? _targetLocks[recentLockTime].CurrentLock : null;
        }
    }

    private static DateTimeOffset FindClosestTime(IEnumerable<DateTimeOffset> keys, DateTimeOffset target)
    {
        if (!keys.Any()) return default;
        return keys.MinBy(k => Math.Abs((k - target).TotalMilliseconds));
    }
    
    private static DateTimeOffset FindMostRecentTime(IEnumerable<DateTimeOffset> keys, DateTimeOffset target)
    {
        var pastKeys = keys.Where(k => k <= target).ToList();
        if (!pastKeys.Any()) return default;
        return pastKeys.Max();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
