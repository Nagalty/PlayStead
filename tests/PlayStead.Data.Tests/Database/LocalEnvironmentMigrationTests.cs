using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class LocalEnvironmentMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.LocalEnvironment-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Fresh_database_contains_local_environment_entries_table()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(version), COUNT(*) FROM schema_migrations WHERE version=30 UNION ALL SELECT 0, COUNT(*) FROM sqlite_master WHERE type='table' AND name='local_environment_entries';";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetInt64(0) >= 30L);
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1L, reader.GetInt64(1));
    }

    [Fact]
    public async Task Legacy_local_artifacts_are_migrated_once_to_installation_scoped_environment()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var game = Guid.NewGuid();
        var installation = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.ToString("O");
        await using (var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO games(game_id,title,created_utc,updated_utc) VALUES($game,'Dune: Awakening',$now,$now);
                INSERT INTO installations(installation_id,game_id,provider,external_id,install_path,last_seen_utc) VALUES($installation,$game,0,'1172710','C:\\Dune',$now);
                INSERT INTO local_artifact_baselines(game_id,artifact_kind,artifact_identity,algorithm,hash,file_count,total_size_bytes,captured_at_utc)
                    VALUES($game,0,'%LOCALAPPDATA%\\DuneSandbox\\Saved\\Config','SHA256','HASH',2,20,$now);
                INSERT INTO local_artifact_snapshots(snapshot_id,game_id,artifact_kind,rule_identity,archive_path,fingerprint_algorithm,fingerprint_hash,file_count,total_size_bytes,created_at_utc,reason)
                    VALUES($snapshot,$game,0,'%LOCALAPPDATA%\\DuneSandbox\\Saved\\Config','C:\\snapshot.zip','SHA256','HASH',2,20,$now,3);
                INSERT INTO local_artifact_snapshots(snapshot_id,game_id,artifact_kind,rule_identity,archive_path,fingerprint_algorithm,fingerprint_hash,file_count,total_size_bytes,created_at_utc,reason)
                    VALUES($invalid,$game,0,'%LOCALAPPDATA%\\DuneSandbox\\Saved\\Config','','SHA256','HASH',2,20,$now,3);
                INSERT INTO user_defined_local_artifacts(artifact_id,game_id,artifact_kind,path,display_name,created_at_utc)
                    VALUES($user,$game,2,'C:\\Dune\\Mods','Mods',$now);
                DELETE FROM schema_migrations WHERE version>=33;
                """;
            command.Parameters.AddWithValue("$game", game.ToString());
            command.Parameters.AddWithValue("$installation", installation.ToString());
            command.Parameters.AddWithValue("$snapshot", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$invalid", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$user", Guid.NewGuid().ToString());
            command.Parameters.AddWithValue("$now", now);
            await command.ExecuteNonQueryAsync();
        }

        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        await using var verify = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False");
        await verify.OpenAsync();
        var count = verify.CreateCommand();
        count.CommandText = "SELECT (SELECT COUNT(*) FROM local_environment_entries WHERE installation_id=$installation), (SELECT COUNT(*) FROM local_environment_baselines WHERE installation_id=$installation), (SELECT COUNT(*) FROM local_file_snapshots WHERE installation_id=$installation), (SELECT COUNT(*) FROM local_artifact_baselines WHERE game_id=$game);";
        count.Parameters.AddWithValue("$installation", installation.ToString());
        count.Parameters.AddWithValue("$game", game.ToString());
        await using var reader = await count.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(1L, reader.GetInt64(2));
        Assert.Equal(1L, reader.GetInt64(3));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
