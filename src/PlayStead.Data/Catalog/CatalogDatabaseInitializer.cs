using Microsoft.Data.Sqlite;

namespace PlayStead.Data.Catalog;

public sealed class CatalogDatabaseInitializer
{
    private const int TargetVersion = 3;
    private static readonly string[] MigrationFiles = ["001_catalog_initial.sql", "002_catalog_genres.sql", "003_catalog_media.sql"];
    private readonly CatalogDatabaseOptions _options;

    public CatalogDatabaseInitializer(CatalogDatabaseOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_options.CatalogPath)
            ?? throw new InvalidOperationException("Catalog path has no parent directory.");
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(_options.BackupsDirectory);

        var exists = File.Exists(_options.CatalogPath) && new FileInfo(_options.CatalogPath).Length > 0;
        var current = exists ? await ReadVersionAsync(cancellationToken) : 0;
        if (current >= TargetVersion) return;

        string? backup = null;
        if (exists)
        {
            backup = Path.Combine(_options.BackupsDirectory,
                $"catalog.db.pre-migration-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.bak");
            File.Copy(_options.CatalogPath, backup, overwrite: false);
        }

        try
        {
            await using var connection = new SqliteConnection($"Data Source={_options.CatalogPath};Pooling=False");
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            for (var version = current + 1; version <= TargetVersion; version++)
            {
                var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = ReadMigration(MigrationFiles[version - 1]);
                await command.ExecuteNonQueryAsync(cancellationToken);

                var record = connection.CreateCommand();
                record.Transaction = (SqliteTransaction)transaction;
                record.CommandText = "INSERT INTO catalog_schema_migrations(version, applied_utc) VALUES ($version, $utc);";
                record.Parameters.AddWithValue("$version", version);
                record.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
                await record.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (backup is not null && File.Exists(backup))
                File.Copy(backup, _options.CatalogPath, overwrite: true);
            throw;
        }
    }

    private async Task<int> ReadVersionAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={_options.CatalogPath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM catalog_schema_migrations;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static string ReadMigration(string migrationFile)
    {
        var assembly = typeof(CatalogDatabaseInitializer).Assembly;
        var name = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(migrationFile, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded migration resource '{name}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
