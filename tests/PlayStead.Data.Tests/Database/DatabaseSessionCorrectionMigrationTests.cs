using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class DatabaseSessionCorrectionMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_fresh_database_creates_current_schema_with_traceable_session_corrections()
    {
        Directory.CreateDirectory(_root);

        var databasePath =
            Path.Combine(
                _root,
                "playstead.db");

        var options =
            new DatabaseOptions(
                databasePath,
                Path.Combine(
                    _root,
                    "Backups"));

        await new DatabaseInitializer(options)
            .InitializeAsync(
                CancellationToken.None);

        await using var connection =
            new SqliteConnection(
                $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var versionCommand =
            connection.CreateCommand();

        versionCommand.CommandText =
            "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";

        Assert.Equal(
23,Convert.ToInt32(
                await versionCommand.ExecuteScalarAsync()));

        var tableCommand =
            connection.CreateCommand();

        tableCommand.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = 'session_corrections';
            """;

        Assert.Equal(
            1,
            Convert.ToInt32(
                await tableCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Initialize_upgrades_v3_to_current_schema_without_mutating_observed_session_data()
    {
        Directory.CreateDirectory(_root);

        var databasePath =
            Path.Combine(
                _root,
                "playstead.db");

        var sessionId =
            Guid.Parse(
                "99999999-1111-4444-8888-999999999999");

        var gameId =
            Guid.Parse(
                "99999999-2222-4444-8888-999999999999");

        await CreateV3DatabaseAsync(
            databasePath,
            gameId,
            sessionId);

        var options =
            new DatabaseOptions(
                databasePath,
                Path.Combine(
                    _root,
                    "Backups"));

        await new DatabaseInitializer(options)
            .InitializeAsync(
                CancellationToken.None);

        await using var connection =
            new SqliteConnection(
                $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var versionCommand =
            connection.CreateCommand();

        versionCommand.CommandText =
            "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";

        Assert.Equal(
23,Convert.ToInt32(
                await versionCommand.ExecuteScalarAsync()));

        var sessionCommand =
            connection.CreateCommand();

        sessionCommand.CommandText = """
            SELECT
                observed_started_at_utc,
                observed_ended_at_utc
            FROM game_sessions
            WHERE session_id = $sessionId;
            """;

        sessionCommand.Parameters.AddWithValue(
            "$sessionId",
            sessionId.ToString());

        await using var reader =
            await sessionCommand.ExecuteReaderAsync();

        Assert.True(
            await reader.ReadAsync());

        Assert.Equal(
            "2026-09-13T01:00:00.0000000+00:00",
            reader.GetString(0));

        Assert.Equal(
            "2026-09-13T02:00:00.0000000+00:00",
            reader.GetString(1));
    }

    private static async Task CreateV3DatabaseAsync(
        string databasePath,
        Guid gameId,
        Guid sessionId)
    {
        await DiscoveryDatabaseFixture.CreateSchemaAsync(databasePath, 3, CancellationToken.None);
        await using var connection =
            new SqliteConnection(
                $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var command =
            connection.CreateCommand();

        command.CommandText = """
            INSERT INTO games(
                game_id,
                title,
                is_hidden,
                created_utc,
                updated_utc)
            VALUES(
                $gameId,
                'Preserved Game',
                0,
                '2026-09-13T00:00:00.0000000+00:00',
                '2026-09-13T00:00:00.0000000+00:00'
            );

            INSERT INTO game_sessions(
                session_id,
                game_id,
                observed_started_at_utc,
                last_seen_at_utc,
                observed_ended_at_utc,
                state,
                end_reason,
                detection_source,
                created_at_utc,
                updated_at_utc)
            VALUES(
                $sessionId,
                $gameId,
                '2026-09-13T01:00:00.0000000+00:00',
                '2026-09-13T02:00:00.0000000+00:00',
                '2026-09-13T02:00:00.0000000+00:00',
                1,
                0,
                0,
                '2026-09-13T01:00:00.0000000+00:00',
                '2026-09-13T02:00:00.0000000+00:00'
            );
            """;

        command.Parameters.AddWithValue(
            "$gameId",
            gameId.ToString());

        command.Parameters.AddWithValue(
            "$sessionId",
            sessionId.ToString());

        await command.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }
}
