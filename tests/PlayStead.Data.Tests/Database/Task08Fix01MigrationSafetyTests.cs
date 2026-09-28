using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class Task08Fix01MigrationSafetyTests : IDisposable
{
    private static readonly Guid GameId =
        Guid.Parse("89898989-8989-4989-8989-898989898989");

    private static readonly Guid SessionId =
        Guid.Parse("90909090-9090-4090-8090-909090909090");

    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Task08Fix01.MigrationSafety",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Existing_v4_correction_is_upgraded_to_traceable_schema_without_losing_observed_data()
    {
        Directory.CreateDirectory(_root);

        var databasePath =
            Path.Combine(
                _root,
                "playstead.db");

        await CreateV4DatabaseAsync(
            databasePath);

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
18,
            Convert.ToInt32(
                await versionCommand.ExecuteScalarAsync()));

        var columnsCommand =
            connection.CreateCommand();

        columnsCommand.CommandText =
            "PRAGMA table_info(session_corrections);";

        var columns =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        await using (var reader =
            await columnsCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                columns.Add(
                    reader.GetString(1));
            }
        }

        Assert.Contains(
            "correction_id",
            columns);

        Assert.Contains(
            "reason",
            columns);

        Assert.Contains(
            "created_at_utc",
            columns);

        var correctionCommand =
            connection.CreateCommand();

        correctionCommand.CommandText = """
            SELECT
                correction_id,
                session_id,
                corrected_started_at_utc,
                corrected_ended_at_utc,
                reason,
                created_at_utc
            FROM session_corrections
            WHERE session_id = $sessionId;
            """;

        correctionCommand.Parameters.AddWithValue(
            "$sessionId",
            SessionId.ToString());

        await using (var reader =
            await correctionCommand.ExecuteReaderAsync())
        {
            Assert.True(
                await reader.ReadAsync());

            Assert.True(
                Guid.TryParse(
                    reader.GetString(0),
                    out var correctionId));

            Assert.NotEqual(
                Guid.Empty,
                correctionId);

            Assert.Equal(
                SessionId.ToString(),
                reader.GetString(1));

            Assert.Equal(
                "2026-09-13T07:55:00.0000000+00:00",
                reader.GetString(2));

            Assert.Equal(
                "2026-09-13T09:05:00.0000000+00:00",
                reader.GetString(3));

            Assert.True(
                reader.IsDBNull(4));

            Assert.Equal(
                "2026-09-13T10:00:00.0000000+00:00",
                reader.GetString(5));
        }

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
            SessionId.ToString());

        await using var sessionReader =
            await sessionCommand.ExecuteReaderAsync();

        Assert.True(
            await sessionReader.ReadAsync());

        Assert.Equal(
            "2026-09-13T08:00:00.0000000+00:00",
            sessionReader.GetString(0));

        Assert.Equal(
            "2026-09-13T09:00:00.0000000+00:00",
            sessionReader.GetString(1));
    }

    private static async Task CreateV4DatabaseAsync(
        string databasePath)
    {
        await DiscoveryDatabaseFixture.CreateSchemaAsync(databasePath, 4, CancellationToken.None);
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
                'Migration Safety Game',
                0,
                '2026-09-13T07:00:00.0000000+00:00',
                '2026-09-13T07:00:00.0000000+00:00'
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
                '2026-09-13T08:00:00.0000000+00:00',
                '2026-09-13T09:00:00.0000000+00:00',
                '2026-09-13T09:00:00.0000000+00:00',
                1,
                0,
                0,
                '2026-09-13T08:00:00.0000000+00:00',
                '2026-09-13T09:00:00.0000000+00:00'
            );

            INSERT INTO session_corrections(
                session_id,
                corrected_started_at_utc,
                corrected_ended_at_utc,
                corrected_at_utc)
            VALUES(
                $sessionId,
                '2026-09-13T07:55:00.0000000+00:00',
                '2026-09-13T09:05:00.0000000+00:00',
                '2026-09-13T10:00:00.0000000+00:00'
            );
            """;

        command.Parameters.AddWithValue(
            "$gameId",
            GameId.ToString());

        command.Parameters.AddWithValue(
            "$sessionId",
            SessionId.ToString());

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
