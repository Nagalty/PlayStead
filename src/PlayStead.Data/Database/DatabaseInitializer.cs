using Microsoft.Data.Sqlite;

namespace PlayStead.Data.Database;

public sealed class DatabaseInitializer
{
    private const int TargetVersion = 1;
    private readonly DatabaseOptions _options;

    public DatabaseInitializer(DatabaseOptions options)
    {
        _options = options;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var databaseDirectory = Path.GetDirectoryName(_options.DatabasePath)
            ?? throw new InvalidOperationException("Database path has no parent directory.");

        Directory.CreateDirectory(databaseDirectory);
        Directory.CreateDirectory(_options.BackupsDirectory);

        var databaseExists =
            File.Exists(_options.DatabasePath) &&
            new FileInfo(_options.DatabasePath).Length > 0;

        var currentVersion = databaseExists
            ? await ReadCurrentVersionAsync(cancellationToken)
            : 0;

        if (currentVersion >= TargetVersion)
        {
            return;
        }

        if (databaseExists)
        {
            var backupPath = Path.Combine(
                _options.BackupsDirectory,
                $"playstead.db.pre-migration-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.bak");

            File.Copy(_options.DatabasePath, backupPath, overwrite: false);
        }

        await ApplyMigrationAsync(cancellationToken);
    }

    private async Task<int> ReadCurrentVersionAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Pooling=False");

        await connection.OpenAsync(cancellationToken);
        return await GetCurrentVersionAsync(connection, cancellationToken);
    }

    private async Task ApplyMigrationAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={_options.DatabasePath};Pooling=False");

        await connection.OpenAsync(cancellationToken);

        var currentVersion = await GetCurrentVersionAsync(connection, cancellationToken);
        if (currentVersion >= TargetVersion)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var migration = connection.CreateCommand();
        migration.Transaction = (SqliteTransaction)transaction;
        migration.CommandText = ReadEmbeddedMigration("001_initial.sql");
        await migration.ExecuteNonQueryAsync(cancellationToken);

        var record = connection.CreateCommand();
        record.Transaction = (SqliteTransaction)transaction;
        record.CommandText =
            "INSERT OR IGNORE INTO schema_migrations(version, applied_utc) VALUES (1, $utc);";
        record.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
        await record.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<int> GetCurrentVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var exists = connection.CreateCommand();
        exists.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_migrations';";

        var hasTable = Convert.ToInt32(
            await exists.ExecuteScalarAsync(cancellationToken)) == 1;

        if (!hasTable)
        {
            return 0;
        }

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";

        return Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken));
    }

    private static string ReadEmbeddedMigration(string fileName)
    {
        var assembly = typeof(DatabaseInitializer).Assembly;

        var resourceName = assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith(fileName, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded migration resource '{resourceName}' was not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
