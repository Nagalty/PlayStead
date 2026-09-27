using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class DatabaseSteamEvidenceMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_fresh_database_keeps_Steam_evidence_tables_in_current_schema()
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

        var version = Convert.ToInt32(
            await versionCommand.ExecuteScalarAsync());

        Assert.Equal(16, version);

        var tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN (
                  'steam_local_evidence',
                  'steam_remote_evidence'
              );
            """;

        var tableCount = Convert.ToInt32(
            await tablesCommand.ExecuteScalarAsync());

        Assert.Equal(2, tableCount);
    }

    [Fact]
    public async Task Initialize_upgrades_existing_v1_database_to_current_schema_without_losing_existing_data()
    {
        Directory.CreateDirectory(_root);

        var databasePath = Path.Combine(_root, "playstead.db");
        var backupsDirectory = Path.Combine(_root, "Backups");

        await CreateV1DatabaseAsync(databasePath);

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

        var version = Convert.ToInt32(
            await versionCommand.ExecuteScalarAsync());

        Assert.Equal(16, version);

        var titleCommand = connection.CreateCommand();
        titleCommand.CommandText =
            "SELECT title FROM games WHERE game_id = 'game-1';";

        var title = Convert.ToString(
            await titleCommand.ExecuteScalarAsync());

        Assert.Equal("Existing Game", title);

        var tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN (
                  'steam_local_evidence',
                  'steam_remote_evidence'
              );
            """;

        var tableCount = Convert.ToInt32(
            await tablesCommand.ExecuteScalarAsync());

        Assert.Equal(2, tableCount);
    }

    private static async Task CreateV1DatabaseAsync(
        string databasePath)
    {
        await DiscoveryDatabaseFixture.CreateSchemaAsync(databasePath, 1, CancellationToken.None);
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
                'game-1',
                'Existing Game',
                0,
                '2026-09-12T00:00:00.0000000+00:00',
                '2026-09-12T00:00:00.0000000+00:00'
            );
            """;

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
