using Microsoft.Data.Sqlite;
using PlayStead.Data.Catalog;

namespace PlayStead.Data.Tests.Catalog;

public sealed class CatalogDatabaseInitializerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_creates_empty_versioned_catalog()
    {
        Directory.CreateDirectory(_root);

        var options = new CatalogDatabaseOptions(
            Path.Combine(_root, "catalog.db"),
            Path.Combine(_root, "Backups"));

        var initializer =
            new CatalogDatabaseInitializer(options);

        await initializer.InitializeAsync(
            CancellationToken.None);

        Assert.True(
            File.Exists(options.CatalogPath));

        Assert.Equal(
            3,
            await ReadSchemaVersionAsync(
                options.CatalogPath));

        Assert.Equal(
            0L,
            await ReadCatalogVersionAsync(
                options.CatalogPath));

        Assert.True(
            await TableExistsAsync(
                options.CatalogPath,
                "catalog_contents"));

        Assert.True(
            await TableExistsAsync(
                options.CatalogPath,
                "catalog_provider_refs"));

        Assert.True(
            await TableExistsAsync(
                options.CatalogPath,
                "catalog_aliases"));

        Assert.True(
            await TableExistsAsync(
                options.CatalogPath,
                "catalog_relations"));
    }

    [Fact]
    public async Task Initialize_is_idempotent()
    {
        Directory.CreateDirectory(_root);
        var options = new CatalogDatabaseOptions(Path.Combine(_root, "catalog.db"),
            Path.Combine(_root, "Backups"));
        var initializer = new CatalogDatabaseInitializer(options);

        await initializer.InitializeAsync(CancellationToken.None);
        var before = File.GetLastWriteTimeUtc(options.CatalogPath);
        await initializer.InitializeAsync(CancellationToken.None);

        Assert.Equal(3, await ReadSchemaVersionAsync(options.CatalogPath));
        Assert.Equal(0L, await ReadCatalogVersionAsync(options.CatalogPath));
        Assert.Equal(before, File.GetLastWriteTimeUtc(options.CatalogPath));
    }

    [Fact]
    public async Task Provider_reference_identity_is_unique_globally()
    {
        Directory.CreateDirectory(_root);
        var options = new CatalogDatabaseOptions(Path.Combine(_root, "catalog.db"),
            Path.Combine(_root, "Backups"));
        await new CatalogDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={options.CatalogPath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO catalog_contents(internal_content_id, public_id, content_kind,
                canonical_title, normalized_title, status)
            VALUES ('11111111-1111-1111-1111-111111111111', 'PlayStead-000001', 1,
                'Game A', 'game a', 1),
                   ('22222222-2222-2222-2222-222222222222', 'PlayStead-000002', 1,
                'Game B', 'game b', 1);
            INSERT INTO catalog_provider_refs(provider, external_id, content_id,
                provenance, confidence, observed_at_utc)
            VALUES (1, 'same', '11111111-1111-1111-1111-111111111111', 1, 2, '2026-01-01T00:00:00Z');
            INSERT INTO catalog_provider_refs(provider, external_id, content_id,
                provenance, confidence, observed_at_utc)
            VALUES (1, 'same', '22222222-2222-2222-2222-222222222222', 1, 2, '2026-01-01T00:00:00Z');
            """;

        await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Schema_constraints_reject_invalid_content_and_relation_rows()
    {
        Directory.CreateDirectory(_root);
        var options = new CatalogDatabaseOptions(Path.Combine(_root, "catalog.db"),
            Path.Combine(_root, "Backups"));
        await new CatalogDatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        await using var connection = new SqliteConnection($"Data Source={options.CatalogPath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO catalog_contents(internal_content_id, public_id, content_kind,
                canonical_title, normalized_title, status, redirect_target_id)
            VALUES ('11111111-1111-1111-1111-111111111111', 'PlayStead-000001', 1,
                'Game', 'game', 1, '11111111-1111-1111-1111-111111111111');
            """;
        await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
    }

    private static async Task<int> ReadSchemaVersionAsync(
        string databasePath)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(MAX(version), 0)
            FROM catalog_schema_migrations;
            """;

        return Convert.ToInt32(
            await command.ExecuteScalarAsync());
    }

    private static async Task<long> ReadCatalogVersionAsync(
        string databasePath)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT catalog_version
            FROM catalog_metadata
            WHERE singleton_id = 1;
            """;

        return Convert.ToInt64(
            await command.ExecuteScalarAsync());
    }

    private static async Task<bool> TableExistsAsync(
        string databasePath,
        string tableName)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = $tableName;
            """;

        command.Parameters.AddWithValue(
            "$tableName",
            tableName);

        return Convert.ToInt32(
            await command.ExecuteScalarAsync()) == 1;
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
