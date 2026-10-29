using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;
using PlayStead.Data.Database;
using PlayStead.Data.LocalEnvironment;

namespace PlayStead.Data.Tests.LocalEnvironment;

public sealed class SqliteLocalEnvironmentBaselineStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.LocalEnvironmentBaseline-" + Guid.NewGuid().ToString("N"));
    private readonly InstallationId _installationId = InstallationId.New();

    [Fact]
    public async Task Baseline_entries_and_metadata_round_trip_and_replace_transactionally()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        await InsertInstallationAsync(options);
        var store = new SqliteLocalEnvironmentBaselineStore(options);
        var first = new LocalEnvironmentBaseline(
            _installationId,
            DateTimeOffset.Parse("2026-10-07T10:00:00Z"),
            DateTimeOffset.Parse("2026-10-07T10:00:00Z"),
            [
                new(LocalEnvironmentEntryKind.Config, "C:\\Games\\HLL\\config.ini", LocalEnvironmentPathKind.File, true, DateTimeOffset.Parse("2026-10-06T10:00:00Z"), new Dictionary<string, string> { ["FileCount"] = "1" }),
                new(LocalEnvironmentEntryKind.Save, "C:\\Games\\HLL\\Saved", LocalEnvironmentPathKind.Directory, true, null, new Dictionary<string, string> { ["FileCount"] = "3", ["TotalSizeBytes"] = "42" }),
                new(LocalEnvironmentEntryKind.Mod, "C:\\Games\\HLL\\BepInEx", LocalEnvironmentPathKind.Directory, true, null, new Dictionary<string, string> { ["ComponentKind"] = "BepInEx", ["ComponentName"] = "BepInEx", ["Version"] = "6.0" })
            ]);
        await store.ReplaceAsync(first, CancellationToken.None);
        var persisted = await store.GetAsync(_installationId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(first.CreatedAtUtc, persisted!.CreatedAtUtc);
        Assert.Equal(3, persisted.Entries.Count);
        Assert.Contains(persisted.Entries, x => x.Kind == LocalEnvironmentEntryKind.Save && x.Metadata!["TotalSizeBytes"] == "42");
        Assert.Contains(persisted.Entries, x => x.Kind == LocalEnvironmentEntryKind.Mod && x.Metadata!["ComponentKind"] == "BepInEx");

        var replacement = first with
        {
            UpdatedAtUtc = DateTimeOffset.Parse("2026-10-07T11:00:00Z"),
            Entries = [new(LocalEnvironmentEntryKind.Config, "C:\\Games\\HLL\\config.ini", LocalEnvironmentPathKind.File, true, null, new Dictionary<string, string> { ["FileCount"] = "2" })]
        };
        await store.ReplaceAsync(replacement, CancellationToken.None);
        var replaced = await store.GetAsync(_installationId, CancellationToken.None);
        Assert.NotNull(replaced);
        Assert.Equal(replacement.UpdatedAtUtc, replaced!.UpdatedAtUtc);
        Assert.Single(replaced.Entries);
        Assert.Equal("2", replaced.Entries[0].Metadata!["FileCount"]);
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
