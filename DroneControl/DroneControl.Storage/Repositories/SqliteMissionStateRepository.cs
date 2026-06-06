using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace DroneControl.Storage.Repositories;

public class SqliteMissionStateRepository : IMissionStateRepository
{
    private readonly SqliteDatabase _db;
    private readonly ILogger<SqliteMissionStateRepository> _logger;

    public SqliteMissionStateRepository(SqliteDatabase db, ILogger<SqliteMissionStateRepository> logger)
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
            CREATE TABLE IF NOT EXISTS mission_states (
                SessionId TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                State TEXT NOT NULL,
                WaypointId TEXT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    public async Task SaveStateTransitionAsync(string sessionId, DateTimeOffset timestamp, MissionState newState, Guid? currentWaypointId)
    {
        using var conn = new SqliteConnection($"Data Source={_db.DatabasePath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO mission_states (SessionId, Timestamp, State, WaypointId) 
            VALUES (@s, @t, @st, @w)";
        cmd.Parameters.AddWithValue("@s", sessionId);
        cmd.Parameters.AddWithValue("@t", timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("@st", newState.ToString());
        cmd.Parameters.AddWithValue("@w", currentWaypointId.HasValue ? currentWaypointId.Value.ToString() : DBNull.Value);
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<(DateTimeOffset Timestamp, MissionState State, Guid? WaypointId)>> LoadStateTransitionsAsync(string sessionId)
    {
        var results = new List<(DateTimeOffset, MissionState, Guid?)>();
        using var conn = new SqliteConnection($"Data Source={_db.DatabasePath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Timestamp, State, WaypointId FROM mission_states WHERE SessionId = @s ORDER BY Timestamp ASC";
        cmd.Parameters.AddWithValue("@s", sessionId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var ts = DateTimeOffset.Parse(reader.GetString(0));
            var state = Enum.Parse<MissionState>(reader.GetString(1));
            Guid? wId = reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2));
            results.Add((ts, state, wId));
        }
        return results;
    }
}
