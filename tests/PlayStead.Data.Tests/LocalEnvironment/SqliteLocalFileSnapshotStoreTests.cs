using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Data.Database;
using PlayStead.Data.LocalEnvironment;

namespace PlayStead.Data.Tests.LocalEnvironment;

public sealed class SqliteLocalFileSnapshotStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.FileSnapshots-" + Guid.NewGuid().ToString("N"));
    private readonly InstallationId _installationId = InstallationId.New();

    [Fact]
    public async Task Metadata_round_trips_isolated_and_deletes_cleanly()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        await InsertInstallationAsync(options);
        var store = new SqliteLocalFileSnapshotStore(options);
        var snapshot = new LocalFileSnapshot(Guid.NewGuid(), _installationId, Guid.NewGuid(), "C:\\Games\\settings.ini", DateTimeOffset.Parse("2026-10-07T12:00:00Z"), LocalFileSnapshotReason.Manual, null, 12, new string('A', 64), "safe/settings.ini", false, new Dictionary<string, string> { ["Fixture"] = "yes" });
        await store.AddAsync(snapshot, CancellationToken.None);
        var persisted = await store.GetAsync(snapshot.Id, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(snapshot.Id, persisted!.Id);
        Assert.Equal(snapshot.InstallationId, persisted.InstallationId);
        Assert.Equal(snapshot.OriginalPath, persisted.OriginalPath);
        Assert.Equal(snapshot.Sha256, persisted.Sha256);
        Assert.Equal("yes", persisted.Metadata!["Fixture"]);
        Assert.Single(await store.GetAsync(_installationId, CancellationToken.None));
        await store.DeleteAsync(snapshot.Id, CancellationToken.None);
        Assert.Null(await store.GetAsync(snapshot.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Latest_local_environment_migration_is_applied_without_blob_storage()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version) FROM schema_migrations;";
        Assert.Equal(34L, (long)(await command.ExecuteScalarAsync())!);
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('local_file_snapshots') WHERE name='blob';";
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    private async Task InsertInstallationAsync(DatabaseOptions options)
    {
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO games(game_id,title,created_utc,updated_utc) VALUES($game,'Fixture',$now,$now); INSERT INTO installations(installation_id,game_id,provider,external_id,install_path,last_seen_utc) VALUES($installation,$game,0,'fixture','C:\\\\Fixture',$now);";
        command.Parameters.AddWithValue("$game", GameId.New().Value.ToString());
        command.Parameters.AddWithValue("$installation", _installationId.Value.ToString());
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
