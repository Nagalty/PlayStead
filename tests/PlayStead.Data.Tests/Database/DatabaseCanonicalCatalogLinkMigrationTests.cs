using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class DatabaseCanonicalCatalogLinkMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Version6_database_migrates_to_version7_with_nullable_canonical_content_link()
    {
        Directory.CreateDirectory(_root);

        var databasePath = Path.Combine(_root, "playstead.db");
        var options = new DatabaseOptions(
            databasePath,
            Path.Combine(_root, "Backups"));

        await CreateVersion6FixtureAsync(databasePath);

        await new DatabaseInitializer(options)
            .InitializeAsync(CancellationToken.None);

        Assert.Equal(
            8,
            await ReadSchemaVersionAsync(databasePath));

        Assert.True(
            await ColumnExistsAsync(
                databasePath,
                "games",
                "canonical_content_id"));

        Assert.Equal(
            "Existing Game",
            await ReadTitleAsync(databasePath));

        Assert.Null(
            await ReadCanonicalContentIdAsync(databasePath));
    }

    private static async Task CreateVersion6FixtureAsync(
        string databasePath)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE schema_migrations(
                version INTEGER PRIMARY KEY,
                applied_utc TEXT NOT NULL
            );

            CREATE TABLE games(
                game_id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                is_hidden INTEGER NOT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            INSERT INTO schema_migrations(version, applied_utc)
            VALUES
                (1, '2026-09-01T00:00:00.0000000+00:00'),
                (2, '2026-09-01T00:00:00.0000000+00:00'),
                (3, '2026-09-01T00:00:00.0000000+00:00'),
                (4, '2026-09-01T00:00:00.0000000+00:00'),
                (5, '2026-09-01T00:00:00.0000000+00:00'),
                (6, '2026-09-01T00:00:00.0000000+00:00');

            INSERT INTO games(
                game_id,
                title,
                is_hidden,
                created_utc,
                updated_utc)
            VALUES(
                '11111111-1111-1111-1111-111111111111',
                'Existing Game',
                0,
                '2026-09-01T00:00:00.0000000+00:00',
                '2026-09-01T00:00:00.0000000+00:00');
            """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ReadSchemaVersionAsync(
        string databasePath)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";

        return Convert.ToInt32(
            await command.ExecuteScalarAsync());
    }

    private static async Task<bool> ColumnExistsAsync(
        string databasePath,
        string table,
        string column)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            if (string.Equals(
                reader.GetString(1),
                column,
                StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string> ReadTitleAsync(
        string databasePath)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT title FROM games LIMIT 1;";

        return Convert.ToString(
            await command.ExecuteScalarAsync())!;
    }

    private static async Task<string?> ReadCanonicalContentIdAsync(
        string databasePath)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT canonical_content_id FROM games LIMIT 1;";

        var value = await command.ExecuteScalarAsync();

        return value is null || value is DBNull
            ? null
            : Convert.ToString(value);
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
