using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class DatabaseMigrationBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_backs_up_existing_database_once_before_migration()
    {
        Directory.CreateDirectory(_root);

        var databasePath = Path.Combine(_root, "playstead.db");
        var backupsDirectory = Path.Combine(_root, "Backups");

        await CreateLegacyDatabaseAsync(databasePath);

        var sut = new DatabaseInitializer(
            new DatabaseOptions(databasePath, backupsDirectory));

        await sut.InitializeAsync(CancellationToken.None);

        var backupsAfterMigration = Directory
            .GetFiles(backupsDirectory, "playstead.db.pre-migration-*.bak");

        var backupPath = Assert.Single(backupsAfterMigration);

        await AssertBackupContainsOnlyPreMigrationStateAsync(backupPath);

        await sut.InitializeAsync(CancellationToken.None);

        var backupsAfterSecondInitialization = Directory
            .GetFiles(backupsDirectory, "playstead.db.pre-migration-*.bak");

        Assert.Single(backupsAfterSecondInitialization);
    }

    private static async Task CreateLegacyDatabaseAsync(string databasePath)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE legacy_marker (
                value TEXT NOT NULL
            );

            INSERT INTO legacy_marker(value)
            VALUES ('before-migration');
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertBackupContainsOnlyPreMigrationStateAsync(
        string backupPath)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={backupPath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync();

        var markerCommand = connection.CreateCommand();
        markerCommand.CommandText =
            "SELECT value FROM legacy_marker LIMIT 1;";

        var marker = Convert.ToString(
            await markerCommand.ExecuteScalarAsync());

        Assert.Equal("before-migration", marker);

        var migrationsTableCommand = connection.CreateCommand();
        migrationsTableCommand.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = 'schema_migrations';
            """;

        var migrationsTableCount = Convert.ToInt32(
            await migrationsTableCommand.ExecuteScalarAsync());

        Assert.Equal(0, migrationsTableCount);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
