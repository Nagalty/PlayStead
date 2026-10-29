using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Data.Database;
using PlayStead.Data.LocalEnvironment;

namespace PlayStead.Data.Tests.LocalEnvironment;

public sealed class SqliteLocalEnvironmentStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.LocalEnvironmentStore-" + Guid.NewGuid().ToString("N"));
    private readonly InstallationId _installationId = InstallationId.New();

    [Fact]
    public async Task Source_confidence_and_user_override_survive_persistence()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        await InsertInstallationAsync(options);
        var store = new SqliteLocalEnvironmentStore(options);
        var detected = Entry(LocalEnvironmentSource.Catalog, LocalEnvironmentConfidence.Detected, false);
        await store.ReplaceDetectedAsync(_installationId, [detected], CancellationToken.None);
        var user = Entry(LocalEnvironmentSource.User, LocalEnvironmentConfidence.Confirmed, true) with { Path = "override.ini" };
        await store.UpsertUserOverrideAsync(user, CancellationToken.None);

        var entries = await store.GetEntriesAsync(_installationId, CancellationToken.None);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, x => x.Source == LocalEnvironmentSource.Catalog && x.Confidence == LocalEnvironmentConfidence.Detected);
        Assert.Contains(entries, x => x.IsUserOverride && x.Path == "override.ini");
    }

    [Fact]
    public async Task Last_modified_timestamp_survives_persistence()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        await InsertInstallationAsync(options);
        var modified = DateTimeOffset.UtcNow.AddMinutes(-2);
        var entry = Entry(LocalEnvironmentSource.Provider, LocalEnvironmentConfidence.Confirmed, false) with { LastModifiedUtc = modified };
        var store = new SqliteLocalEnvironmentStore(options);
        await store.ReplaceDetectedAsync(_installationId, [entry], CancellationToken.None);

        var persisted = Assert.Single(await store.GetEntriesAsync(_installationId, CancellationToken.None));
        Assert.Equal(modified, persisted.LastModifiedUtc);
    }

    private LocalEnvironmentEntry Entry(LocalEnvironmentSource source, LocalEnvironmentConfidence confidence, bool user) =>
        new(Guid.NewGuid(), _installationId, LocalEnvironmentEntryKind.Config, "config.ini", LocalEnvironmentPathKind.File, confidence, source, false, DateTimeOffset.UtcNow, null, null, user);

    private async Task InsertInstallationAsync(DatabaseOptions options)
    {
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
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
