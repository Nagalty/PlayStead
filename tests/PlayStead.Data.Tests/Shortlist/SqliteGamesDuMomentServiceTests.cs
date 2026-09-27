using PlayStead.Core.Library;
using PlayStead.Core.Shortlist;
using PlayStead.Data.Database;
using PlayStead.Data.Shortlist;

namespace PlayStead.Data.Tests.Shortlist;

public sealed class SqliteGamesDuMomentServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Add_remove_reload_and_reorder_preserve_explicit_order()
    {
        var options = await InitializeAsync();
        var service = new SqliteGamesDuMomentService(options);
        var first = new GameId(Guid.NewGuid()); var second = new GameId(Guid.NewGuid());
        Assert.Equal(GamesDuMomentAddResult.Added, await service.AddAsync(first, default));
        Assert.Equal(GamesDuMomentAddResult.Added, await service.AddAsync(second, default));
        Assert.Equal(GamesDuMomentAddResult.AlreadyMember, await service.AddAsync(first, default));
        await service.ReorderAsync(new[] { second, first }, default);
        var reloaded = new SqliteGamesDuMomentService(options);
        Assert.Equal(new[] { second, first }, (await reloaded.GetAsync(default)).Select(x => x.GameId));
        Assert.True(await reloaded.RemoveAsync(second, default));
        Assert.False(await reloaded.RemoveAsync(second, default));
        Assert.Equal(first, Assert.Single(await reloaded.GetAsync(default)).GameId);
    }

    [Fact]
    public async Task Add_returns_full_without_eviction()
    {
        var options = await InitializeAsync(); var service = new SqliteGamesDuMomentService(options);
        var ids = Enumerable.Range(0, 6).Select(_ => new GameId(Guid.NewGuid())).ToArray();
        foreach (var id in ids.Take(5)) Assert.Equal(GamesDuMomentAddResult.Added, await service.AddAsync(id, default));
        Assert.Equal(GamesDuMomentAddResult.Full, await service.AddAsync(ids[5], default));
        Assert.Equal(5, (await service.GetAsync(default)).Count);
    }

    [Fact]
    public async Task Concurrent_duplicate_add_is_idempotent()
    {
        var options = await InitializeAsync();
        var first = new SqliteGamesDuMomentService(options);
        var second = new SqliteGamesDuMomentService(options);
        var id = new GameId(Guid.NewGuid());
        var results = await Task.WhenAll(first.AddAsync(id, default), second.AddAsync(id, default));
        Assert.All(results, result => Assert.Contains(result, new[] { GamesDuMomentAddResult.Added, GamesDuMomentAddResult.AlreadyMember }));
        Assert.Single(await first.GetAsync(default));
    }

    private async Task<DatabaseOptions> InitializeAsync()
    { Directory.CreateDirectory(_root); var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups")); await new DatabaseInitializer(options).InitializeAsync(default); return options; }
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
