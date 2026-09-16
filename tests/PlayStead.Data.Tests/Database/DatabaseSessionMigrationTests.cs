using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class DatabaseSessionMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_fresh_database_keeps_session_and_signature_tables_in_current_schema()
    {
        Directory.CreateDirectory(_root);

        var databasePath = Path.Combine(_root, "playstead.db");
        var backupsDirectory = Path.Combine(_root, "Backups");

        var sut = new DatabaseInitializer(
            new DatabaseOptions(
                databasePath,
                backupsDirectory));

        await sut.InitializeAsync(CancellationToken.None);

        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var versionCommand = connection.CreateCommand();
        versionCommand.CommandText =
            "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";

        Assert.Equal(
            9,
            Convert.ToInt32(
                await versionCommand.ExecuteScalarAsync()));

        var tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN (
                  'game_sessions',
                  'process_signatures',
                  'process_signature_entries'
              );
            """;

        Assert.Equal(
            3,
            Convert.ToInt32(
                await tablesCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Initialize_upgrades_existing_v2_database_to_current_schema_without_losing_games()
    {
        Directory.CreateDirectory(_root);

        var databasePath = Path.Combine(_root, "playstead.db");
        var backupsDirectory = Path.Combine(_root, "Backups");

        var gameId = Guid.Parse(
            "55555555-5555-5555-5555-555555555555");

        await CreateV2DatabaseAsync(
            databasePath,
            gameId);

        var sut = new DatabaseInitializer(
            new DatabaseOptions(
                databasePath,
                backupsDirectory));

        await sut.InitializeAsync(CancellationToken.None);

        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var versionCommand = connection.CreateCommand();
        versionCommand.CommandText =
            "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";

        Assert.Equal(
            9,
            Convert.ToInt32(
                await versionCommand.ExecuteScalarAsync()));

        var titleCommand = connection.CreateCommand();
        titleCommand.CommandText =
            "SELECT title FROM games WHERE game_id = $gameId;";
        titleCommand.Parameters.AddWithValue(
            "$gameId",
            gameId.ToString());

        Assert.Equal(
            "Preserved Game",
            Convert.ToString(
                await titleCommand.ExecuteScalarAsync()));

        var tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN (
                  'game_sessions',
                  'process_signatures',
                  'process_signature_entries'
              );
            """;

        Assert.Equal(
            3,
            Convert.ToInt32(
                await tablesCommand.ExecuteScalarAsync()));
    }

    private static async Task CreateV2DatabaseAsync(
        string databasePath,
        Guid gameId)
    {
        await DiscoveryDatabaseFixture.CreateSchemaAsync(databasePath, 2, CancellationToken.None);
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO games(
                game_id,
                title,
                is_hidden,
                created_utc,
                updated_utc)
            VALUES (
                $gameId,
                'Preserved Game',
                0,
                '2026-09-12T00:00:00.0000000+00:00',
                '2026-09-12T00:00:00.0000000+00:00'
            );
            """;

        command.Parameters.AddWithValue(
            "$gameId",
            gameId.ToString());

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
