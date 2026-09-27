using Microsoft.Data.Sqlite;
using PlayStead.Core.GameBuildHistory;
using PlayStead.Core.Library;
using PlayStead.Data.Database;
using PlayStead.Data.GameBuildHistory;

namespace PlayStead.Data.Tests.GameBuildHistory;

public sealed class SqliteGameBuildHistoryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));
    private readonly GameId _game = GameId.New();

    [Fact]
    public async Task History_round_trips_and_deduplicates_same_build()
    {
        Directory.CreateDirectory(_root);
        var databasePath = Path.Combine(_root, "playstead.db");
        var options = new DatabaseOptions(databasePath, Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES($id,'Fixture',0,$utc,$utc);";
            command.Parameters.AddWithValue("$id", _game.Value.ToString("D"));
            command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteGameBuildHistoryStore(options);
        var first = new GameBuildObservation(_game, ProviderKind.Steam, "1284210", "build-a", DateTimeOffset.Parse("2026-09-01T00:00:00Z"));
        Assert.True(await store.AppendIfChangedAsync(first, CancellationToken.None));
        Assert.False(await store.AppendIfChangedAsync(first with { ObservedAtUtc = first.ObservedAtUtc.AddHours(1) }, CancellationToken.None));
        Assert.True(await store.AppendIfChangedAsync(first with { BuildId = "build-b", ObservedAtUtc = first.ObservedAtUtc.AddDays(1) }, CancellationToken.None));

        var history = await store.GetHistoryAsync(_game, ProviderKind.Steam, CancellationToken.None);
        Assert.Equal(2, history.Count);
        Assert.Equal("build-b", (await store.GetLatestAsync(_game, ProviderKind.Steam, CancellationToken.None))!.BuildId);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
