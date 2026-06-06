using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

namespace DroneControl.Integrations.Tracking;

/// <summary>
/// A lightweight intersection-over-union tracker satisfying the base requirements for SORT.
/// Provides stable TrackIds across frames without requiring external C++ dependencies or heavy matrix libraries.
/// </summary>
public class IouTrackingProvider : ITrackingProvider
{
    public string ProviderName => "IoU Tracking Provider";

    private readonly Dictionary<int, TrackData> _activeTracks = new();
    private int _nextTrackId = 1;
    private readonly double _iouThreshold = 0.3;
    private readonly int _maxLostFrames = 5;

    public Task<IReadOnlyList<TrackedObject>> UpdateAsync(
        IReadOnlyList<DetectionResult> detections, 
        CancellationToken cancellationToken = default)
    {
        var currentTracks = new List<TrackedObject>();

        // Age all tracks
        foreach (var track in _activeTracks.Values)
        {
            track.FramesSinceSeen++;
        }

        // Match detections to existing tracks using greedy IoU
        var unmatchedDetections = new List<DetectionResult>();

        foreach (var detection in detections)
        {
            var bestMatchId = -1;
            var bestIou = _iouThreshold;

            foreach (var kvp in _activeTracks)
            {
                var iou = CalculateIou(kvp.Value.LastDetection, detection);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    bestMatchId = kvp.Key;
                }
            }

            if (bestMatchId != -1)
            {
                // Found a match
                var track = _activeTracks[bestMatchId];
                track.LastDetection = detection;
                track.FramesSinceSeen = 0;
                
                currentTracks.Add(new TrackedObject(
                    bestMatchId,
                    detection,
                    false
                ));
            }
            else
            {
                unmatchedDetections.Add(detection);
            }
        }

        // Create new tracks for unmatched detections
        foreach (var detection in unmatchedDetections)
        {
            var newId = _nextTrackId++;
            _activeTracks[newId] = new TrackData
            {
                LastDetection = detection,
                FramesSinceSeen = 0
            };

            currentTracks.Add(new TrackedObject(
                newId,
                detection,
                false
            ));
        }

        // Include stale tracks that are not yet dead
        foreach (var kvp in _activeTracks)
        {
            if (kvp.Value.FramesSinceSeen > 0 && kvp.Value.FramesSinceSeen <= _maxLostFrames)
            {
                currentTracks.Add(new TrackedObject(
                    kvp.Key,
                    kvp.Value.LastDetection,
                    true
                ));
            }
        }

        // Remove dead tracks
        var deadIds = _activeTracks.Where(k => k.Value.FramesSinceSeen > _maxLostFrames).Select(k => k.Key).ToList();
        foreach (var id in deadIds)
        {
            _activeTracks.Remove(id);
        }

        return Task.FromResult<IReadOnlyList<TrackedObject>>(currentTracks);
    }

    private static double CalculateIou(DetectionResult boxA, DetectionResult boxB)
    {
        var xA = Math.Max(boxA.X, boxB.X);
        var yA = Math.Max(boxA.Y, boxB.Y);
        var xB = Math.Min(boxA.X + boxA.Width, boxB.X + boxB.Width);
        var yB = Math.Min(boxA.Y + boxA.Height, boxB.Y + boxB.Height);

        var interArea = Math.Max(0, xB - xA) * Math.Max(0, yB - yA);
        if (interArea == 0) return 0;

        var boxAArea = boxA.Width * boxA.Height;
        var boxBArea = boxB.Width * boxB.Height;

        return interArea / (boxAArea + boxBArea - interArea);
    }

    private class TrackData
    {
        public DetectionResult LastDetection { get; set; } = default!;
        public int FramesSinceSeen { get; set; }
    }
}
