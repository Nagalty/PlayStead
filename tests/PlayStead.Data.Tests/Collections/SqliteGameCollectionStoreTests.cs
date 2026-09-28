using Microsoft.Data.Sqlite;
using PlayStead.Core.Collections;
using PlayStead.Core.Library;
using PlayStead.Data.Collections;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Collections;

public sealed class SqliteGameCollectionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Collections_and_memberships_persist_and_delete_cascades()
    {
        var options = await InitializeAsync();
        var gameId = new GameId(Guid.NewGuid());
        await InsertGameAsync(options, gameId);
        var store = new SqliteGameCollectionStore(options);
        var favorites = await store.CreateCollectionAsync("  Favoris  ", default);
        await store.SetMembershipAsync(favorites.Id, gameId, true, default);

        var reloaded = new SqliteGameCollectionStore(options);
        Assert.Equal("Favoris", Assert.Single(await reloaded.GetCollectionsAsync(default)).Name);
        Assert.Contains(favorites.Id, await reloaded.GetMembershipsAsync(gameId, default));
        await reloaded.SetMembershipAsync(favorites.Id, gameId, false, default);
        Assert.Empty(await reloaded.GetMembershipsAsync(gameId, default));
        await reloaded.SetMembershipAsync(favorites.Id, gameId, true, default);
        Assert.True(await reloaded.DeleteCollectionAsync(favorites.Id, default));
        Assert.Empty(await reloaded.GetMembershipsAsync(default));
    }

    [Fact]
    public async Task Names_are_trimmed_and_case_insensitive_duplicates_are_rejected()
    {
        var options = await InitializeAsync();
        var store = new SqliteGameCollectionStore(options);
        await store.CreateCollectionAsync("Favoris", default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateCollectionAsync(" favoris ", default));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateCollectionAsync("   ", default));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateCollectionAsync(new string('x', 61), default));
    }

    [Fact]
    public async Task Rename_preserves_memberships_and_duplicate_membership_is_idempotent()
    {
        var options = await InitializeAsync();
        var gameId = new GameId(Guid.NewGuid());
        await InsertGameAsync(options, gameId);
        var store = new SqliteGameCollectionStore(options);
        var collection = await store.CreateCollectionAsync("À finir", default);
        await store.SetMembershipAsync(collection.Id, gameId, true, default);
        await store.SetMembershipAsync(collection.Id, gameId, true, default);
        var renamed = await store.RenameCollectionAsync(collection.Id, "À terminer", default);
        Assert.Equal("À terminer", renamed.Name);
        Assert.Contains(collection.Id, await store.GetMembershipsAsync(gameId, default));
        Assert.Single(await store.GetMembershipsAsync(default));
    }

    private async Task<DatabaseOptions> InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options).InitializeAsync(default);
        return options;
    }

    private static async Task InsertGameAsync(DatabaseOptions options, GameId gameId)
    {
        await using var connection = new SqliteConnection($"Data Source={options.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO games(game_id,title,created_utc,updated_utc) VALUES($id,'Test', $utc, $utc);";
        command.Parameters.AddWithValue("$id", gameId.ToString());
        command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
