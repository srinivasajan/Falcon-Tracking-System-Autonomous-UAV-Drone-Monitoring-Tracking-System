using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Data.Sqlite;

namespace DroneControl.Storage;

public sealed class SqliteTargetLockRepository : ITargetLockRepository
{
    private readonly SqliteDatabase _database;
    private readonly ITrackingRepository _trackingRepository;

    public SqliteTargetLockRepository(SqliteDatabase database, ITrackingRepository trackingRepository)
    {
        _database = database;
        _trackingRepository = trackingRepository;
    }

    public async Task SaveTargetLockEventAsync(string sessionId, TargetLockEvent lockEvent, CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO target_locks (
                session_id, timestamp, track_id, event_type
            ) VALUES (
                $session_id, $timestamp, $track_id, $event_type
            );
            """;

        command.Parameters.AddWithValue("$session_id", sessionId);
        command.Parameters.AddWithValue("$timestamp", lockEvent.Timestamp.ToString("O"));
        command.Parameters.AddWithValue("$track_id", lockEvent.CurrentLock?.TrackId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$event_type", lockEvent.CurrentLock != null ? "AcquiredOrUpdated" : "Cleared");

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<DateTimeOffset, TargetLockEvent>> LoadAllTargetLocksForSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // Get tracks to reconstruct lock state
        var allTracks = await _trackingRepository.LoadAllTracksForSessionAsync(sessionId, cancellationToken);
        var trackMap = new Dictionary<int, TrackedObject>();
        foreach (var dict in allTracks.Values)
        {
            foreach (var t in dict)
            {
                trackMap[t.TrackId] = t; // we only need the latest or any to reconstruct the object structure, actually we just need one detection.
                // Replay will just use TrackId to update its local lock state!
            }
        }

        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT timestamp, track_id, event_type
            FROM target_locks
            WHERE session_id = $session_id
            ORDER BY timestamp ASC;
            """;
        command.Parameters.AddWithValue("$session_id", sessionId);

        var dictionary = new Dictionary<DateTimeOffset, TargetLockEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var timestampStr = reader.GetString(0);
            if (!DateTimeOffset.TryParse(timestampStr, out var timestamp))
                continue;

            var eventType = reader.GetString(2);
            TargetLock? targetLock = null;

            if (eventType != "Cleared" && !reader.IsDBNull(1))
            {
                var trackId = reader.GetInt32(1);
                if (trackMap.TryGetValue(trackId, out var track))
                {
                    targetLock = new TargetLock(trackId, track.Detection, timestamp);
                }
            }

            dictionary[timestamp] = new TargetLockEvent(timestamp, targetLock);
        }

        return dictionary;
    }
}
