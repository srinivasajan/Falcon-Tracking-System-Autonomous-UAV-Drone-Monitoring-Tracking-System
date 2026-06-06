using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

using System.Threading.Channels;

namespace DroneControl.Storage;

public sealed class SqliteTelemetryRecorder : ITelemetryRecorder, IAsyncDisposable
{
    private readonly SqliteDatabase _database;
    private string? _activeSessionId;
    
    private readonly Channel<(string SessionId, TelemetrySnapshot Snapshot)> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _flushTask;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public string? ActiveSessionId => _activeSessionId;

    public SqliteTelemetryRecorder(SqliteDatabase database)
    {
        _database = database;
        _channel = Channel.CreateUnbounded<(string SessionId, TelemetrySnapshot Snapshot)>(new UnboundedChannelOptions { SingleReader = true });
        _flushTask = Task.Run(FlushLoopAsync);
    }

    public async Task<string> StartRecordingAsync(string name, string? videoPath = null, CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString();
        var startTime = DateTimeOffset.UtcNow;

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await _database.InitializeAsync(cancellationToken);
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO telemetry_sessions (id, name, start_time, video_path)
            VALUES ($id, $name, $start_time, $video_path);
            """;
        command.Parameters.AddWithValue("$id", sessionId);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$start_time", startTime.ToString("O"));
        command.Parameters.AddWithValue("$video_path", videoPath ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);

            _activeSessionId = sessionId;
            return sessionId;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (_activeSessionId == null) return;

        var endTime = DateTimeOffset.UtcNow;

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await _database.InitializeAsync(cancellationToken);
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE telemetry_sessions
            SET end_time = $end_time
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", _activeSessionId);
        command.Parameters.AddWithValue("$end_time", endTime.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);

            _activeSessionId = null;
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public Task RecordAsync(TelemetrySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (_activeSessionId != null)
        {
            _channel.Writer.TryWrite((_activeSessionId, snapshot));
        }
        return Task.CompletedTask;
    }

    private async Task FlushLoopAsync()
    {
        var buffer = new List<(string SessionId, TelemetrySnapshot Snapshot)>();
        try
        {
            while (await _channel.Reader.WaitToReadAsync(_cts.Token))
            {
                while (_channel.Reader.TryRead(out var item))
                {
                    buffer.Add(item);
                    if (buffer.Count >= 100)
                    {
                        try {
                            await FlushBatchAsync(buffer);
                        } catch (Exception ex) {
                            Console.WriteLine($"[SqliteTelemetryRecorder] Flush Error: {ex.Message}");
                        }
                        buffer.Clear();
                    }
                }
                
                // Flush any remaining items if we drained the channel
                if (buffer.Count > 0)
                {
                    try {
                        await FlushBatchAsync(buffer);
                    } catch (Exception ex) {
                        Console.WriteLine($"[SqliteTelemetryRecorder] Flush Error: {ex.Message}");
                    }
                    buffer.Clear();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception outerEx)
        {
            Console.WriteLine($"[SqliteTelemetryRecorder] Critical Loop Error: {outerEx}");
        }
    }

    private async Task FlushBatchAsync(List<(string SessionId, TelemetrySnapshot Snapshot)> batch)
    {
        if (batch.Count == 0) return;

        await _dbLock.WaitAsync(_cts.Token);
        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync(_cts.Token);

        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(_cts.Token);

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO telemetry_snapshots (
                session_id, timestamp, battery_percent, latitude, longitude, speed_mps, altitude_meters, mode
            ) VALUES (
                $session_id, $timestamp, $battery_percent, $latitude, $longitude, $speed_mps, $altitude_meters, $mode
            );
            """;

        var pSessionId = command.Parameters.Add("$session_id", Microsoft.Data.Sqlite.SqliteType.Text);
        var pTimestamp = command.Parameters.Add("$timestamp", Microsoft.Data.Sqlite.SqliteType.Text);
        var pBattery = command.Parameters.Add("$battery_percent", Microsoft.Data.Sqlite.SqliteType.Integer);
        var pLat = command.Parameters.Add("$latitude", Microsoft.Data.Sqlite.SqliteType.Real);
        var pLon = command.Parameters.Add("$longitude", Microsoft.Data.Sqlite.SqliteType.Real);
        var pSpeed = command.Parameters.Add("$speed_mps", Microsoft.Data.Sqlite.SqliteType.Real);
        var pAlt = command.Parameters.Add("$altitude_meters", Microsoft.Data.Sqlite.SqliteType.Real);
        var pMode = command.Parameters.Add("$mode", Microsoft.Data.Sqlite.SqliteType.Text);

        foreach (var item in batch)
        {
            pSessionId.Value = item.SessionId;
            pTimestamp.Value = item.Snapshot.Timestamp.ToString("O");
            pBattery.Value = item.Snapshot.BatteryPercent;
            pLat.Value = item.Snapshot.Latitude;
            pLon.Value = item.Snapshot.Longitude;
            pSpeed.Value = item.Snapshot.SpeedMetersPerSecond;
            pAlt.Value = item.Snapshot.AltitudeMeters;
            pMode.Value = item.Snapshot.Mode;

            await command.ExecuteNonQueryAsync(_cts.Token);
        }

            await transaction.CommitAsync(_cts.Token);
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<TelemetrySession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, start_time, end_time, video_path FROM telemetry_sessions ORDER BY start_time DESC;";

        var sessions = new List<TelemetrySession>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var endTimeStr = reader.IsDBNull(3) ? null : reader.GetString(3);
            DateTimeOffset? endTime = endTimeStr == null ? null : DateTimeOffset.Parse(endTimeStr);
            var videoPath = reader.IsDBNull(4) ? null : reader.GetString(4);

            sessions.Add(new TelemetrySession(
                reader.GetString(0),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2)),
                videoPath,
                endTime
            ));
        }

        return sessions;
    }

    public async Task<IReadOnlyList<TelemetrySnapshot>> LoadReplayAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT timestamp, battery_percent, latitude, longitude, speed_mps, altitude_meters, mode
            FROM telemetry_snapshots
            WHERE session_id = $session_id
            ORDER BY id;
            """;
        command.Parameters.AddWithValue("$session_id", sessionId);

        var snapshots = new List<TelemetrySnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshots.Add(new TelemetrySnapshot(
                DateTimeOffset.Parse(reader.GetString(0)),
                IsConnected: true,
                reader.GetInt32(1),
                reader.GetDouble(2),
                reader.GetDouble(3),
                reader.GetDouble(4),
                reader.GetDouble(5),
                reader.GetString(6)));
        }

        return snapshots;
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        try { await _flushTask; } catch { }
        _cts.Cancel();
        _cts.Dispose();
    }
}
