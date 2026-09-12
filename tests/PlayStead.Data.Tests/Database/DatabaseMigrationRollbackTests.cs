using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class DatabaseMigrationRollbackTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_restores_exact_pre_migration_database_when_migration_fails()
    {
        Directory.CreateDirectory(_root);

        var databasePath = Path.Combine(_root, "playstead.db");
        var backupsDirectory = Path.Combine(_root, "Backups");

        await CreateIncompatibleLegacyDatabaseAsync(databasePath);
        var beforeHash = ComputeSha256(databasePath);

        var sut = new DatabaseInitializer(
            new DatabaseOptions(databasePath, backupsDirectory));

        await Assert.ThrowsAsync<SqliteException>(
            () => sut.InitializeAsync(CancellationToken.None));

        SqliteConnection.ClearAllPools();

        var backups = Directory.GetFiles(
            backupsDirectory,
            "playstead.db.pre-migration-*.bak");

        Assert.Single(backups);
        Assert.Equal(beforeHash, ComputeSha256(databasePath));
        Assert.Equal(beforeHash, ComputeSha256(backups[0]));
    }

    private static async Task CreateIncompatibleLegacyDatabaseAsync(
        string databasePath)
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

            -- Deliberately incompatible with schema v1.
            -- CREATE TABLE IF NOT EXISTS will keep this shape,
            -- then the v1 index creation will fail because the expected
            -- provider/external_id/install_path columns do not exist.
            CREATE TABLE installations (
                legacy_value TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream));
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
