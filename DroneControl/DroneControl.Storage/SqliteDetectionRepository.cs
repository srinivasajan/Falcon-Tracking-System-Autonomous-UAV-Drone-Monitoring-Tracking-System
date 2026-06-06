using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

namespace DroneControl.Storage;

public sealed class SqliteDetectionRepository : IDetectionRepository
{
    private readonly SqliteDatabase _database;

    public SqliteDetectionRepository(SqliteDatabase database)
    {
        _database = database;
    }

    public async Task SaveDetectionsAsync(string sessionId, DateTimeOffset timestamp, IReadOnlyList<DetectionResult> detections, CancellationToken cancellationToken = default)
    {
        if (detections.Count == 0) return;

        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO vision_detections (
                session_id, timestamp, detection_id, object_type, confidence, x, y, width, height
            ) VALUES (
                $session_id, $timestamp, $detection_id, $object_type, $confidence, $x, $y, $width, $height
            );
            """;

        var pSessionId = command.Parameters.Add("$session_id", Microsoft.Data.Sqlite.SqliteType.Text);
        var pTimestamp = command.Parameters.Add("$timestamp", Microsoft.Data.Sqlite.SqliteType.Text);
        var pDetectionId = command.Parameters.Add("$detection_id", Microsoft.Data.Sqlite.SqliteType.Text);
        var pObjectType = command.Parameters.Add("$object_type", Microsoft.Data.Sqlite.SqliteType.Text);
        var pConfidence = command.Parameters.Add("$confidence", Microsoft.Data.Sqlite.SqliteType.Real);
        var pX = command.Parameters.Add("$x", Microsoft.Data.Sqlite.SqliteType.Real);
        var pY = command.Parameters.Add("$y", Microsoft.Data.Sqlite.SqliteType.Real);
        var pWidth = command.Parameters.Add("$width", Microsoft.Data.Sqlite.SqliteType.Real);
        var pHeight = command.Parameters.Add("$height", Microsoft.Data.Sqlite.SqliteType.Real);

        pSessionId.Value = sessionId;
        pTimestamp.Value = timestamp.ToString("O");

        foreach (var detection in detections)
        {
            pDetectionId.Value = detection.DetectionId;
            pObjectType.Value = detection.ObjectType.ToString();
            pConfidence.Value = detection.Confidence;
            pX.Value = detection.X;
            pY.Value = detection.Y;
            pWidth.Value = detection.Width;
            pHeight.Value = detection.Height;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DetectionResult>> LoadDetectionsAsync(string sessionId, DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT detection_id, object_type, confidence, x, y, width, height
            FROM vision_detections
            WHERE session_id = $session_id AND timestamp = $timestamp;
            """;
        command.Parameters.AddWithValue("$session_id", sessionId);
        command.Parameters.AddWithValue("$timestamp", timestamp.ToString("O"));

        var results = new List<DetectionResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var objectTypeStr = reader.GetString(1);
            if (!Enum.TryParse<DetectedObjectType>(objectTypeStr, out var objectType))
            {
                objectType = DetectedObjectType.Unknown;
            }

            results.Add(new DetectionResult(
                reader.GetString(0),
                objectType,
                reader.GetDouble(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetDouble(6)
            ));
        }

        return results;
    }

    public async Task<IReadOnlyDictionary<DateTimeOffset, IReadOnlyList<DetectionResult>>> LoadAllDetectionsForSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT timestamp, detection_id, object_type, confidence, x, y, width, height
            FROM vision_detections
            WHERE session_id = $session_id
            ORDER BY timestamp ASC;
            """;
        command.Parameters.AddWithValue("$session_id", sessionId);

        var dictionary = new Dictionary<DateTimeOffset, List<DetectionResult>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var timestampStr = reader.GetString(0);
            if (!DateTimeOffset.TryParse(timestampStr, out var timestamp))
                continue;

            var objectTypeStr = reader.GetString(2);
            if (!Enum.TryParse<DetectedObjectType>(objectTypeStr, out var objectType))
            {
                objectType = DetectedObjectType.Unknown;
            }

            var detection = new DetectionResult(
                reader.GetString(1),
                objectType,
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetDouble(6),
                reader.GetDouble(7)
            );

            if (!dictionary.TryGetValue(timestamp, out var list))
            {
                list = new List<DetectionResult>();
                dictionary[timestamp] = list;
            }
            list.Add(detection);
        }

        return dictionary.ToDictionary(k => k.Key, v => (IReadOnlyList<DetectionResult>)v.Value);
    }
}
