using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class ProviderLaunchMetadataMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.ProviderLaunchMetadata-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Fresh_database_contains_nullable_provider_launch_metadata_column()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('installations') WHERE name='provider_launch_metadata_json' AND \"notnull\"=0;";
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
