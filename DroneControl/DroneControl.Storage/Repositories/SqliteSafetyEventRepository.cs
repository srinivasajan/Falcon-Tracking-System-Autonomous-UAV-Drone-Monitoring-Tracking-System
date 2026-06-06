using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace DroneControl.Storage.Repositories;

public class SqliteSafetyEventRepository : ISafetyEventRepository
{
    private readonly SqliteDatabase _db;
    private readonly ILogger<SqliteSafetyEventRepository> _logger;

    public SqliteSafetyEventRepository(SqliteDatabase db, ILogger<SqliteSafetyEventRepository> logger)
    {
        _db = db;
        _logger = logger;
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var conn = new SqliteConnection($"Data Source={_db.DatabasePath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS safety_events (
                SessionId TEXT NOT NULL,
                Id TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                Level TEXT NOT NULL,
                Source TEXT NOT NULL,
                Description TEXT NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    public async Task SaveSafetyEventAsync(string sessionId, SafetyEvent safetyEvent)
    {
        using var conn = new SqliteConnection($"Data Source={_db.DatabasePath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO safety_events (SessionId, Id, Timestamp, Level, Source, Description) 
            VALUES (@s, @id, @t, @lvl, @src, @desc)";
        cmd.Parameters.AddWithValue("@s", sessionId);
        cmd.Parameters.AddWithValue("@id", safetyEvent.Id.ToString());
        cmd.Parameters.AddWithValue("@t", safetyEvent.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@lvl", safetyEvent.Level.ToString());
        cmd.Parameters.AddWithValue("@src", safetyEvent.Source);
        cmd.Parameters.AddWithValue("@desc", safetyEvent.Description);
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<SafetyEvent>> LoadSafetyEventsAsync(string sessionId, DateTimeOffset start, DateTimeOffset end)
    {
        var results = new List<SafetyEvent>();
        using var conn = new SqliteConnection($"Data Source={_db.DatabasePath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Timestamp, Level, Source, Description FROM safety_events WHERE SessionId = @s AND Timestamp >= @start AND Timestamp <= @end ORDER BY Timestamp ASC";
        cmd.Parameters.AddWithValue("@s", sessionId);
        cmd.Parameters.AddWithValue("@start", start.ToString("O"));
        cmd.Parameters.AddWithValue("@end", end.ToString("O"));
        
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SafetyEvent(
                Guid.Parse(reader.GetString(0)),
                DateTimeOffset.Parse(reader.GetString(1)),
                Enum.Parse<SafetyLevel>(reader.GetString(2)),
                reader.GetString(3),
                reader.GetString(4)
            ));
        }
        return results;
    }
}
