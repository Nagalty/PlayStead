using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Data.Database;
using PlayStead.Data.Identity;

namespace PlayStead.Data.Tests.Identity;

public sealed class SqliteIdentityDecisionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "DecisionStore", Guid.NewGuid().ToString("N"));
    private readonly GameId _game = GameId.New();
    private readonly DatabaseOptions _options;

    public SqliteIdentityDecisionStoreTests()
    {
        Directory.CreateDirectory(_root);
        _options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups"));
    }

    [Fact]
    public async Task Roundtrip_active_queries_and_revoke_history()
    {
        await SetupAsync();
        var store = new SqliteIdentityDecisionStore(_options);
        var now = DateTimeOffset.UtcNow;
        var confirmed = Decision(IdentityDecisionType.UserConfirmed, CatalogContentId.New(), now);
        var rejected = Decision(IdentityDecisionType.UserRejected, CatalogContentId.New(), now);
        await store.InsertAsync(confirmed, CancellationToken.None);
        await store.InsertAsync(rejected, CancellationToken.None);
        Assert.Equal(confirmed, await store.GetActiveConfirmedAsync(_game, CancellationToken.None));
        Assert.Single(await store.ListActiveRejectedAsync(_game, CancellationToken.None));
        Assert.Equal(2, (await store.ListActiveAsync(_game, CancellationToken.None)).Count);
        var revoked = now.AddMinutes(1);
        await store.RevokeAsync(confirmed.DecisionId, revoked, CancellationToken.None);
        Assert.Null(await store.GetActiveConfirmedAsync(_game, CancellationToken.None));
        Assert.Equal(2, await CountRowsAsync());
    }

    [Fact]
    public async Task Duplicate_active_decisions_are_rejected_and_reuse_after_revoke_succeeds()
    {
        await SetupAsync();
        var store = new SqliteIdentityDecisionStore(_options);
        var now = DateTimeOffset.UtcNow;
        var first = Decision(IdentityDecisionType.UserConfirmed, CatalogContentId.New(), now);
        await store.InsertAsync(first, CancellationToken.None);
        await Assert.ThrowsAsync<SqliteException>(() => store.InsertAsync(Decision(IdentityDecisionType.UserConfirmed, CatalogContentId.New(), now), CancellationToken.None));
        await Assert.ThrowsAsync<SqliteException>(() => store.InsertAsync(first with { DecisionId = IdentityDecisionId.New(), DecisionType = IdentityDecisionType.UserRejected }, CancellationToken.None));
        await store.RevokeAsync(first.DecisionId, now.AddMinutes(1), CancellationToken.None);
        await store.InsertAsync(first with { DecisionId = IdentityDecisionId.New(), DecisionType = IdentityDecisionType.UserRejected }, CancellationToken.None);
    }

    [Fact]
    public async Task Cancelled_token_is_propagated()
    {
        await SetupAsync();
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SqliteIdentityDecisionStore(_options).ListActiveAsync(_game, source.Token));
    }

    private GameIdentityDecision Decision(IdentityDecisionType type, CatalogContentId content, DateTimeOffset now) => new(IdentityDecisionId.New(), _game, content, type, now, now, null);
    private async Task SetupAsync() { await new DatabaseInitializer(_options).InitializeAsync(CancellationToken.None); await using var c = await OpenAsync(); using var cmd = c.CreateCommand(); cmd.CommandText = "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES ($id,'G',0,$u,$u);"; cmd.Parameters.AddWithValue("$id", _game.ToString()); cmd.Parameters.AddWithValue("$u", DateTimeOffset.UtcNow.ToString("O")); await cmd.ExecuteNonQueryAsync(); }
    private async Task<long> CountRowsAsync() { await using var c = await OpenAsync(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM game_identity_decisions;"; return Convert.ToInt64(await cmd.ExecuteScalarAsync()); }
    private async Task<SqliteConnection> OpenAsync() { var c = new SqliteConnection($"Data Source={_options.DatabasePath};Pooling=False;Foreign Keys=True"); await c.OpenAsync(); return c; }
    public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
