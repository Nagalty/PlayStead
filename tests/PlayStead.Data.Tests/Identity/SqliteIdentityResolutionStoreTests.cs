using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Data.Database;
using PlayStead.Data.Identity;

namespace PlayStead.Data.Tests.Identity;

public sealed class SqliteIdentityResolutionStoreTests : IDisposable
{
    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse("2026-09-16T18:00:00Z");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        "IdentityStore",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetAsync_returns_null_when_game_has_no_resolution()
    {
        var (store, _) = await CreateStoreAsync("absent.db", Game(1));

        var result = await store.GetAsync(
            Game(1),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task First_GetOrCreate_creates_new_provisional_resolution()
    {
        var (store, _) = await CreateStoreAsync("first.db", Game(1));

        var result = await store.GetOrCreateProvisionalAsync(
            Game(1),
            NoMatch("1874880"),
            ObservedAt,
            CancellationToken.None);

        Assert.Equal(IdentityResolutionState.New, result.State);
        Assert.NotNull(result.ProvisionalIdentityId);
        Assert.StartsWith(
            "PS-TEMP-",
            result.ProvisionalIdentityId.Value.Value);
        Assert.Null(result.CandidateContentId);
    }

    [Fact]
    public async Task Second_GetOrCreate_for_same_game_returns_same_provisional_id()
    {
        var (store, _) = await CreateStoreAsync("same-game.db", Game(1));

        var first = await store.GetOrCreateProvisionalAsync(
            Game(1), NoMatch("1874880"), ObservedAt,
            CancellationToken.None);
        var second = await store.GetOrCreateProvisionalAsync(
            Game(1), NoMatch("1874880"), ObservedAt.AddHours(1),
            CancellationToken.None);

        Assert.Equal(
            first.ProvisionalIdentityId,
            second.ProvisionalIdentityId);
        Assert.Equal(first.CreatedAtUtc, second.CreatedAtUtc);
    }

    [Fact]
    public async Task Reloaded_store_returns_same_provisional_id()
    {
        var (store, options) =
            await CreateStoreAsync("reload.db", Game(1));
        var first = await store.GetOrCreateProvisionalAsync(
            Game(1), NoMatch("1874880"), ObservedAt,
            CancellationToken.None);

        var reloaded = new SqliteIdentityResolutionStore(options);
        var second = await reloaded.GetOrCreateProvisionalAsync(
            Game(1), NoMatch("1874880"), ObservedAt.AddHours(1),
            CancellationToken.None);

        Assert.Equal(
            first.ProvisionalIdentityId,
            second.ProvisionalIdentityId);
    }

    [Fact]
    public async Task Different_games_receive_different_provisional_ids()
    {
        var (store, _) = await CreateStoreAsync(
            "different-games.db",
            Game(1),
            Game(2));

        var first = await store.GetOrCreateProvisionalAsync(
            Game(1), NoMatch("1874880"), ObservedAt,
            CancellationToken.None);
        var second = await store.GetOrCreateProvisionalAsync(
            Game(2), NoMatch("9999999"), ObservedAt,
            CancellationToken.None);

        Assert.NotEqual(
            first.ProvisionalIdentityId,
            second.ProvisionalIdentityId);
    }

    [Fact]
    public async Task Upsert_match_confirmed_preserves_existing_provisional_id()
    {
        var (store, _) = await CreateStoreAsync("preserve.db", Game(1));
        var provisional = await store.GetOrCreateProvisionalAsync(
            Game(1), NoMatch("1874880"), ObservedAt,
            CancellationToken.None);
        var contentId = CatalogContentId.New();
        var confirmedAt = ObservedAt.AddHours(1);

        await store.UpsertAsync(
            new GameIdentityResolution(
                Game(1),
                ProvisionalIdentityId: null,
                IdentityResolutionState.MatchConfirmed,
                contentId,
                ExactMatch("1874880", contentId),
                confirmedAt,
                confirmedAt),
            CancellationToken.None);

        var result = await store.GetAsync(
            Game(1),
            CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(
            provisional.ProvisionalIdentityId,
            result.ProvisionalIdentityId);
        Assert.Equal(provisional.CreatedAtUtc, result.CreatedAtUtc);
        Assert.Equal(confirmedAt, result.UpdatedAtUtc);
    }

    [Fact]
    public async Task Upsert_direct_match_confirmed_allows_null_provisional_id()
    {
        var (store, _) = await CreateStoreAsync("direct.db", Game(1));
        var contentId = CatalogContentId.New();
        var resolution = new GameIdentityResolution(
            Game(1),
            ProvisionalIdentityId: null,
            IdentityResolutionState.MatchConfirmed,
            contentId,
            ExactMatch("1874880", contentId),
            ObservedAt,
            ObservedAt);

        await store.UpsertAsync(
            resolution,
            CancellationToken.None);

        var result = await store.GetOrCreateProvisionalAsync(
            Game(1),
            NoMatch("1874880"),
            ObservedAt.AddHours(1),
            CancellationToken.None);
        Assert.Null(result.ProvisionalIdentityId);
        Assert.Equal(IdentityResolutionState.MatchConfirmed, result.State);
        Assert.Equal(contentId, result.CandidateContentId);
    }

    [Fact]
    public async Task ListAsync_returns_each_persisted_resolution()
    {
        var (store, _) = await CreateStoreAsync(
            "list.db",
            Game(1),
            Game(2));
        var first = await store.GetOrCreateProvisionalAsync(
            Game(1), NoMatch("1874880"), ObservedAt,
            CancellationToken.None);
        var second = await store.GetOrCreateProvisionalAsync(
            Game(2), NoMatch("9999999"), ObservedAt,
            CancellationToken.None);

        var result = await store.ListAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(first, result);
        Assert.Contains(second, result);
    }

    [Fact]
    public async Task Already_cancelled_token_is_propagated()
    {
        var (store, _) = await CreateStoreAsync("cancelled.db", Game(1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetOrCreateProvisionalAsync(
                Game(1),
                NoMatch("1874880"),
                ObservedAt,
                cancellation.Token));
    }

    [Fact]
    public async Task Evidence_json_round_trips_exactly()
    {
        var (store, _) = await CreateStoreAsync("evidence.db", Game(1));
        var contentId = CatalogContentId.New();
        var evidence = ExactMatch("1874880", contentId);
        var resolution = new GameIdentityResolution(
            Game(1),
            ProvisionalIdentityId: null,
            IdentityResolutionState.MatchConfirmed,
            contentId,
            evidence,
            ObservedAt,
            ObservedAt);

        await store.UpsertAsync(resolution, CancellationToken.None);
        var restored = await store.GetAsync(
            Game(1),
            CancellationToken.None);

        Assert.NotNull(restored);
        Assert.Equal(evidence, restored.Evidence);
    }

    private async Task<(SqliteIdentityResolutionStore Store,
        DatabaseOptions Options)> CreateStoreAsync(
        string fileName,
        params GameId[] gameIds)
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(
            Path.Combine(_root, fileName),
            Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options)
            .InitializeAsync(CancellationToken.None);

        await using var connection = new SqliteConnection(
            $"Data Source={options.DatabasePath};Pooling=False");
        await connection.OpenAsync();

        foreach (var gameId in gameIds)
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES ($gameId,$title,0,$utc,$utc);";
            command.Parameters.AddWithValue("$gameId", gameId.ToString());
            command.Parameters.AddWithValue("$title", gameId.ToString());
            command.Parameters.AddWithValue("$utc", ObservedAt.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        return (new SqliteIdentityResolutionStore(options), options);
    }

    private static IdentityResolutionEvidence NoMatch(
        string externalId) =>
        new(
            IdentityResolutionEvidenceKind.NoExactProviderRefMatch,
            CatalogProviderKind.Steam,
            externalId,
            MatchedContentId: null);

    private static IdentityResolutionEvidence ExactMatch(
        string externalId,
        CatalogContentId contentId) =>
        new(
            IdentityResolutionEvidenceKind.ExactProviderRef,
            CatalogProviderKind.Steam,
            externalId,
            contentId);

    private static GameId Game(int index) =>
        new(Guid.Parse(
            $"00000000-0000-4000-8000-{index:000000000000}"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
