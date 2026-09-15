using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

internal sealed class DiscoveryDatabaseFixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "Discovery", Guid.NewGuid().ToString("N"));

    public DiscoveryDatabaseFixture()
    {
        Directory.CreateDirectory(_root);
        Options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups"));
    }

    public DatabaseOptions Options { get; }

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        new DatabaseInitializer(Options).InitializeAsync(cancellationToken);

    public static async Task CreateSchemaAsync(string databasePath, int version, CancellationToken cancellationToken)
    {
        if (version is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(version));

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken);
        var assembly = typeof(DatabaseInitializer).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Take(version).ToArray();
        Assert.Equal(version, names.Length);

        for (var index = 0; index < names.Length; index++)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var stream = assembly.GetManifestResourceStream(names[index])!;
            using var reader = new StreamReader(stream);
            using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = await reader.ReadToEndAsync(cancellationToken);
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "INSERT INTO schema_migrations(version, applied_utc) VALUES ($version, $utc);";
            command.Parameters.AddWithValue("$version", index + 1);
            command.Parameters.AddWithValue("$utc", "2026-09-12T00:00:00.0000000+00:00");
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task SeedInstallationAsync(GameId gameId, InstallationId installationId, string rootPath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO games(game_id,title,is_hidden,created_utc,updated_utc)
            VALUES ($game,'Fixture game',0,$utc,$utc);
            INSERT INTO installations(installation_id,game_id,provider,external_id,install_path,installed_size_bytes,is_preferred,is_present,last_seen_utc)
            VALUES ($installation,$game,0,'fixture',$path,NULL,0,1,$utc);
            """;
        command.Parameters.AddWithValue("$game", gameId.ToString());
        command.Parameters.AddWithValue("$installation", installationId.ToString());
        command.Parameters.AddWithValue("$path", rootPath);
        command.Parameters.AddWithValue("$utc", "2026-09-12T00:00:00.0000000+00:00");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={Options.DatabasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
