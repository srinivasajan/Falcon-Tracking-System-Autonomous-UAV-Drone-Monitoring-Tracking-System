using Microsoft.Data.Sqlite;

namespace DroneControl.Storage;

public sealed class SqliteDatabase
{
    private readonly StoragePaths _paths;

    public SqliteDatabase(StoragePaths paths)
    {
        _paths = paths;
    }

    public string DatabasePath => Path.Combine(_paths.RootPath, "dronecontrol.db");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            PRAGMA busy_timeout=5000;

            CREATE TABLE IF NOT EXISTS telemetry_snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                battery_percent INTEGER NOT NULL,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL,
                speed_mps REAL NOT NULL,
                altitude_meters REAL NOT NULL,
                mode TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS telemetry_sessions (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                start_time TEXT NOT NULL,
                video_path TEXT,
                end_time TEXT
            );

            CREATE TABLE IF NOT EXISTS missions (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                created_at TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS vision_detections (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                detection_id TEXT NOT NULL,
                object_type TEXT NOT NULL,
                confidence REAL NOT NULL,
                x REAL NOT NULL,
                y REAL NOT NULL,
                width REAL NOT NULL,
                height REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS vision_tracks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                track_id INTEGER NOT NULL,
                detection_id TEXT NOT NULL,
                is_stale INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS target_locks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                track_id INTEGER,
                event_type TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={DatabasePath};Cache=Shared;Default Timeout=5");
    }
}
