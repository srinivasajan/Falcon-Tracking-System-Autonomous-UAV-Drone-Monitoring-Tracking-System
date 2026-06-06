using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

namespace DroneControl.Storage;

public sealed class SqliteTrackingRepository : ITrackingRepository
{
    private readonly SqliteDatabase _database;
    private readonly IDetectionRepository _detectionRepository;

    public SqliteTrackingRepository(SqliteDatabase database, IDetectionRepository detectionRepository)
    {
        _database = database;
        _detectionRepository = detectionRepository;
    }

    public async Task SaveTracksAsync(string sessionId, DateTimeOffset timestamp, IReadOnlyList<TrackedObject> tracks, CancellationToken cancellationToken = default)
    {
        if (tracks.Count == 0) return;

        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO vision_tracks (
                session_id, timestamp, track_id, detection_id, is_stale
            ) VALUES (
                $session_id, $timestamp, $track_id, $detection_id, $is_stale
            );
            """;

        var pSessionId = command.Parameters.Add("$session_id", Microsoft.Data.Sqlite.SqliteType.Text);
        var pTimestamp = command.Parameters.Add("$timestamp", Microsoft.Data.Sqlite.SqliteType.Text);
        var pTrackId = command.Parameters.Add("$track_id", Microsoft.Data.Sqlite.SqliteType.Integer);
        var pDetectionId = command.Parameters.Add("$detection_id", Microsoft.Data.Sqlite.SqliteType.Text);
        var pIsStale = command.Parameters.Add("$is_stale", Microsoft.Data.Sqlite.SqliteType.Integer);

        pSessionId.Value = sessionId;
        pTimestamp.Value = timestamp.ToString("O");

        foreach (var track in tracks)
        {
            pTrackId.Value = track.TrackId;
            pDetectionId.Value = track.Detection.DetectionId;
            pIsStale.Value = track.IsStale ? 1 : 0;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TrackedObject>> LoadTracksAsync(string sessionId, DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
        // First load detections so we can map them
        var detections = await _detectionRepository.LoadDetectionsAsync(sessionId, timestamp, cancellationToken);
        var detectionMap = new Dictionary<string, DetectionResult>();
        foreach (var d in detections)
        {
            detectionMap[d.DetectionId] = d;
        }

        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT track_id, detection_id, is_stale
            FROM vision_tracks
            WHERE session_id = $session_id AND timestamp = $timestamp;
            """;
        command.Parameters.AddWithValue("$session_id", sessionId);
        command.Parameters.AddWithValue("$timestamp", timestamp.ToString("O"));

        var results = new List<TrackedObject>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var detectionId = reader.GetString(1);
            if (detectionMap.TryGetValue(detectionId, out var detection))
            {
                results.Add(new TrackedObject(
                    reader.GetInt32(0),
                    detection,
                    reader.GetInt32(2) != 0
                ));
            }
        }

        return results;
    }

    public async Task<IReadOnlyDictionary<DateTimeOffset, IReadOnlyList<TrackedObject>>> LoadAllTracksForSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        // For unified loading we need to first get all detections to map to tracks
        var allDetections = await _detectionRepository.LoadAllDetectionsForSessionAsync(sessionId, cancellationToken);
        var detectionMap = new Dictionary<string, DetectionResult>();
        foreach (var dict in allDetections.Values)
        {
            foreach (var d in dict)
            {
                detectionMap[d.DetectionId] = d;
            }
        }

        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT timestamp, track_id, detection_id, is_stale
            FROM vision_tracks
            WHERE session_id = $session_id
            ORDER BY timestamp ASC;
            """;
        command.Parameters.AddWithValue("$session_id", sessionId);

        var dictionary = new Dictionary<DateTimeOffset, List<TrackedObject>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var timestampStr = reader.GetString(0);
            if (!DateTimeOffset.TryParse(timestampStr, out var timestamp))
                continue;

            var detectionId = reader.GetString(2);
            if (detectionMap.TryGetValue(detectionId, out var detection))
            {
                var track = new TrackedObject(
                    reader.GetInt32(1),
                    detection,
                    reader.GetInt32(3) != 0
                );

                if (!dictionary.TryGetValue(timestamp, out var list))
                {
                    list = new List<TrackedObject>();
                    dictionary[timestamp] = list;
                }
                list.Add(track);
            }
        }

        return dictionary.ToDictionary(k => k.Key, v => (IReadOnlyList<TrackedObject>)v.Value);
    }
}
