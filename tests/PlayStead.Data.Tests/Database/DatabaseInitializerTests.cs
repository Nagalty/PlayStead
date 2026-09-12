using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_creates_schema_v1_and_is_idempotent()
    {
        Directory.CreateDirectory(_root);

        var databasePath = Path.Combine(_root, "playstead.db");
        var backupsDirectory = Path.Combine(_root, "Backups");
        var sut = new DatabaseInitializer(
            new DatabaseOptions(databasePath, backupsDirectory));

        await sut.InitializeAsync(CancellationToken.None);
        await sut.InitializeAsync(CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();

        var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT MAX(version) FROM schema_migrations;";
        var version = Convert.ToInt32(await versionCommand.ExecuteScalarAsync());

        Assert.Equal(1, version);

        var tablesCommand = connection.CreateCommand();
        tablesCommand.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('schema_migrations', 'games', 'provider_game_refs', 'installations');
            """;

        var tableCount = Convert.ToInt32(await tablesCommand.ExecuteScalarAsync());
        Assert.Equal(4, tableCount);
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite enables connection pooling by default.
        // Disposed connections can therefore keep a Windows file handle
        // associated with this temporary database until the pool is cleared.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
