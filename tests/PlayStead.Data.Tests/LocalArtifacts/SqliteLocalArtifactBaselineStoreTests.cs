using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Data.Database;
using PlayStead.Data.LocalArtifacts;

namespace PlayStead.Data.Tests.LocalArtifacts;

public sealed class SqliteLocalArtifactBaselineStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "Baselines", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Baseline_round_trips_replaces_and_coexists_by_identity()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var game = GameId.New();
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={options.DatabasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES($id,'Fixture',0,$utc,$utc);";
            command.Parameters.AddWithValue("$id", game.Value.ToString("D"));
            command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        var store = new SqliteLocalArtifactBaselineStore(options);
        var first = new LocalArtifactBaseline(game, GameLocalArtifactKind.Configuration, "rule-a", "SHA256", "A", 1, 10, DateTimeOffset.UtcNow);
        var second = first with { Hash = "B", ArtifactIdentity = "rule-b" };
        await store.UpsertAsync(first, CancellationToken.None);
        await store.UpsertAsync(second, CancellationToken.None);
        await store.UpsertAsync(first with { Hash = "C" }, CancellationToken.None);

        Assert.Equal("C", (await store.GetAsync(game, first.Kind, "rule-a", CancellationToken.None))!.Hash);
        Assert.Equal("B", (await store.GetAsync(game, second.Kind, "rule-b", CancellationToken.None))!.Hash);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
