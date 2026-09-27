using PlayStead.Core.Library;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Data.Database;
using PlayStead.Data.ProviderGameMetadata;
using Metadata = PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata;

namespace PlayStead.Data.Tests.ProviderGameMetadata;

public sealed class SqliteProviderGameMetadataStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlaySteadMetadata", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Round_trip_preserves_unknown_and_empty_collection_states()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var store = new SqliteProviderGameMetadataStore(options);
        var game = GameId.New();
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={options.DatabasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO games(game_id,title,created_utc,updated_utc) VALUES($id,'Game',$now,$now);";
            command.Parameters.AddWithValue("$id", game.Value.ToString("D"));
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
        var value = Metadata.Create(game, ProviderKind.Steam, "123", DateTimeOffset.UtcNow, genres: null, categories: [], shortDescription: "A short editorial description.", onlineCoopMaxPlayers: 4);

        await store.UpsertAsync(value, CancellationToken.None);

        var loaded = await store.GetAsync(game, ProviderKind.Steam, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Null(loaded!.Genres);
        Assert.Empty(loaded.Categories!);
        Assert.Equal("A short editorial description.", loaded.ShortDescription);
        Assert.Equal(4, loaded.OnlineCoopMaxPlayers);
        Assert.Null(loaded.OfflineCoopMaxPlayers);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
