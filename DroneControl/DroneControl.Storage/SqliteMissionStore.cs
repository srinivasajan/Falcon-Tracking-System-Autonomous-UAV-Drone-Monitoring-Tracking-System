using System.Text.Json;
using DroneControl.Core.Abstractions;
using DroneControl.Core.Models;

namespace DroneControl.Storage;

public sealed class SqliteMissionStore : IMissionStore
{
    private readonly SqliteDatabase _database;

    public SqliteMissionStore(SqliteDatabase database)
    {
        _database = database;
    }

    public async Task SaveAsync(MissionPlan mission, CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO missions (id, name, created_at, payload_json)
            VALUES ($id, $name, $created_at, $payload_json)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                created_at = excluded.created_at,
                payload_json = excluded.payload_json;
            """;
        command.Parameters.AddWithValue("$id", mission.Id);
        command.Parameters.AddWithValue("$name", mission.Name);
        command.Parameters.AddWithValue("$created_at", mission.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$payload_json", JsonSerializer.Serialize(mission));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MissionPlan>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        await _database.InitializeAsync(cancellationToken);
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT payload_json FROM missions ORDER BY created_at DESC;";

        var missions = new List<MissionPlan>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var mission = JsonSerializer.Deserialize<MissionPlan>(reader.GetString(0));
            if (mission is not null)
            {
                missions.Add(mission);
            }
        }

        return missions;
    }
}
