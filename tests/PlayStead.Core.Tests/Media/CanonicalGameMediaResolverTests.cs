using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Media;

public sealed class CanonicalGameMediaResolverTests
{
    [Fact]
    public async Task GOG_active_without_logo_uses_linked_Steam_logo()
    {
        var gameId = GameId.New();
        var store = new IdentityStore(
            Identity(gameId, ProviderKind.Gog, "GOG-TEST"),
            Identity(gameId, ProviderKind.Steam, "456"));
        var cache = new Cache();
        var resolver = Create(store, cache, new Provider(ProviderKind.Steam, "456", "steam-logo.png"));

        var path = await resolver.ResolveAndCacheAsync(
            gameId, "Synthetic Game", GameMediaAssetType.Logo, ProviderKind.Gog, default);

        Assert.Equal("steam-logo.png", path);
    }

    [Fact]
    public async Task Active_GOG_logo_wins_when_it_is_valid()
    {
        var gameId = GameId.New();
        var store = new IdentityStore(
            Identity(gameId, ProviderKind.Gog, "GOG-TEST"),
            Identity(gameId, ProviderKind.Steam, "456"));
        var resolver = Create(
            store,
            new Cache(),
            new Provider(ProviderKind.Gog, "GOG-TEST", "gog-logo.png"),
            new Provider(ProviderKind.Steam, "456", "steam-logo.png"));

        var path = await resolver.ResolveAndCacheAsync(
            gameId, "Synthetic Game", GameMediaAssetType.Logo, ProviderKind.Gog, default);

        Assert.Equal("gog-logo.png", path);
    }

    [Fact]
    public async Task Invalid_square_icon_is_rejected()
    {
        var gameId = GameId.New();
        var store = new IdentityStore(Identity(gameId, ProviderKind.Steam, "456"));
        var resolver = Create(
            store,
            new Cache(),
            new Provider(ProviderKind.Steam, "456", "steam_square_icon_v2.webp"));

        var path = await resolver.ResolveAndCacheAsync(
            gameId, "Synthetic Game", GameMediaAssetType.Logo, ProviderKind.Steam, default);

        Assert.Null(path);
    }

    [Fact]
    public async Task Unlinked_same_title_does_not_cross_resolve()
    {
        var gameId = GameId.New();
        var store = new IdentityStore(Identity(gameId, ProviderKind.Gog, "GOG-TEST"));
        var resolver = Create(
            store,
            new Cache(),
            new Provider(ProviderKind.Steam, "456", "steam-logo.png"));

        var path = await resolver.ResolveAndCacheAsync(
            gameId, "Crysis Remastered", GameMediaAssetType.Logo, ProviderKind.Gog, default);

        Assert.Null(path);
    }

    [Fact]
    public async Task Manual_game_can_use_reliably_linked_Steam_logo_without_changing_provider()
    {
        var gameId = GameId.New();
        var store = new IdentityStore(
            Identity(gameId, ProviderKind.Manual, "manual-source"),
            Identity(gameId, ProviderKind.Steam, "456"));
        var resolver = Create(
            store,
            new Cache(),
            new Provider(ProviderKind.Steam, "456", "steam-logo.png"));

        var path = await resolver.ResolveAndCacheAsync(
            gameId, "Manual Game", GameMediaAssetType.Logo, ProviderKind.Manual, default);

        Assert.Equal("steam-logo.png", path);
    }

    [Fact]
    public async Task Epic_game_can_use_reliably_linked_Steam_logo_without_changing_provider()
    {
        var gameId = GameId.New();
        var store = new IdentityStore(
            Identity(gameId, ProviderKind.Epic, "epic-hll"),
            Identity(gameId, ProviderKind.Steam, "3393110"));
        var resolver = Create(
            store,
            new Cache(),
            new Provider(ProviderKind.Steam, "3393110", "steam-logo.png"));

        var path = await resolver.ResolveAndCacheAsync(
            gameId, "Hell Let Loose", GameMediaAssetType.Logo, ProviderKind.Epic, default);

        Assert.Equal("steam-logo.png", path);
    }

    [Fact]
    public async Task Epic_invalid_cached_logo_does_not_block_linked_Steam_enrichment()
    {
        var gameId = GameId.New();
        var store = new IdentityStore(
            Identity(gameId, ProviderKind.Epic, "epic-hll"),
            Identity(gameId, ProviderKind.Steam, "3393110"));
        var cache = new Cache();
        cache.Seed(ProviderKind.Epic, "epic-hll", "epic_square_icon.webp", "https://cdn.test/appicon.webp");
        var resolver = Create(
            store,
            cache,
            new Provider(ProviderKind.Steam, "3393110", "steam-logo.png"));

        var path = await resolver.ResolveAndCacheAsync(
            gameId, "Hell Let Loose", GameMediaAssetType.Logo, ProviderKind.Epic, default);

        Assert.Equal("steam-logo.png", path);
    }

    [Fact]
    public async Task Adding_and_removing_an_identity_recomputes_the_canonical_logo()
    {
        var gameId = GameId.New();
        var store = new IdentityStore(Identity(gameId, ProviderKind.Gog, "GOG-TEST"));
        var cache = new Cache();
        var resolver = Create(
            store,
            cache,
            new Provider(ProviderKind.Steam, "456", "steam-logo.png"));

        Assert.Null(await resolver.ResolveAndCacheAsync(
            gameId, "Synthetic Game", GameMediaAssetType.Logo, ProviderKind.Gog, default));

        store.Add(Identity(gameId, ProviderKind.Steam, "456"));
        Assert.Equal("steam-logo.png", await resolver.ResolveAndCacheAsync(
            gameId, "Synthetic Game", GameMediaAssetType.Logo, ProviderKind.Gog, default));

        Assert.True(store.Remove(gameId, ProviderKind.Steam, "456"));
        Assert.Null(await resolver.ResolveAndCacheAsync(
            gameId, "Synthetic Game", GameMediaAssetType.Logo, ProviderKind.Gog, default));
    }

    [Fact]
    public async Task Cache_hit_and_concurrent_requests_do_not_repeat_enrichment()
    {
        var gameId = GameId.New();
        var store = new IdentityStore(Identity(gameId, ProviderKind.Steam, "456"));
        var cache = new Cache();
        var provider = new Provider(ProviderKind.Steam, "456", "steam-logo.png", delayMs: 25);
        var resolver = Create(store, cache, provider);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => resolver.ResolveAndCacheAsync(
                gameId, "Synthetic Game", GameMediaAssetType.Logo, ProviderKind.Steam, default)));

        Assert.All(results, path => Assert.Equal("steam-logo.png", path));
        Assert.Equal(1, provider.ResolveCalls);
    }

    [Fact]
    public async Task Invalid_cached_square_icon_is_rejected_and_linked_Steam_is_tried()
    {
        var gameId = GameId.New();
        var store = new IdentityStore(
            Identity(gameId, ProviderKind.Gog, "GOG-TEST"),
            Identity(gameId, ProviderKind.Steam, "456"));
        var cache = new Cache();
        cache.Seed(ProviderKind.Gog, "GOG-TEST", "gog-logo.webp", "https://cdn.test/square_icon_v2.webp");
        var resolver = Create(
            store,
            cache,
            new Provider(ProviderKind.Gog, "GOG-TEST", "gog_square_icon_v2.webp"),
            new Provider(ProviderKind.Steam, "456", "steam-logo.png"));

        var path = await resolver.ResolveAndCacheAsync(
            gameId, "Synthetic Game", GameMediaAssetType.Logo, ProviderKind.Gog, default);

        Assert.Equal("steam-logo.png", path);
    }

    private static CanonicalGameMediaResolver Create(
        IdentityStore store,
        Cache cache,
        params IGameMediaProvider[] providers) =>
        new(store, cache, providers);

    private static GameProviderIdentity Identity(
        GameId gameId,
        ProviderKind provider,
        string externalId) =>
        new(
            gameId,
            provider,
            externalId,
            ProviderIdentitySource.ProviderObservation,
            CatalogConfidence.Deterministic,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed class IdentityStore(params GameProviderIdentity[] initial) : IProviderIdentityStore
    {
        private readonly List<GameProviderIdentity> _identities = [.. initial];

        public Task<IReadOnlyList<GameProviderIdentity>> GetByGameIdAsync(
            GameId gameId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameProviderIdentity>>(
                _identities.Where(identity => identity.GameId == gameId).ToArray());

        public Task AssociateAsync(GameProviderIdentity identity, CancellationToken cancellationToken)
        {
            _identities.RemoveAll(existing =>
                existing.GameId == identity.GameId &&
                existing.Provider == identity.Provider &&
                existing.ExternalId == identity.ExternalId);
            _identities.Add(identity);
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(
            GameId gameId,
            ProviderKind provider,
            string externalId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_identities.RemoveAll(identity =>
                identity.GameId == gameId &&
                identity.Provider == provider &&
                identity.ExternalId == externalId) > 0);

        public void Add(GameProviderIdentity identity) => _identities.Add(identity);

        public bool Remove(GameId gameId, ProviderKind provider, string externalId) =>
            _identities.RemoveAll(identity =>
                identity.GameId == gameId && identity.Provider == provider && identity.ExternalId == externalId) > 0;
    }

    private sealed class Cache : IGameMediaCache
    {
        private readonly Dictionary<(ProviderKind Provider, string Id), (string Path, string? SourceUri)> _paths = new();

        public string? TryGetPath(GameMediaIdentity identity, GameMediaAssetType assetType) =>
            assetType == GameMediaAssetType.Logo &&
            _paths.TryGetValue((identity.Provider, identity.ProviderGameId), out var entry)
                ? entry.Path
                : null;

        public string? TryGetSourceUri(GameMediaIdentity identity, GameMediaAssetType assetType) =>
            assetType == GameMediaAssetType.Logo &&
            _paths.TryGetValue((identity.Provider, identity.ProviderGameId), out var entry)
                ? entry.SourceUri
                : null;

        public Task<string> StoreAsync(
            GameMediaIdentity identity,
            GameMediaPayload payload,
            CancellationToken cancellationToken)
        {
            var path = payload.Source;
            _paths[(identity.Provider, identity.ProviderGameId)] = (path, payload.SourceUri?.AbsoluteUri);
            return Task.FromResult(path);
        }

        public void Seed(ProviderKind provider, string externalId, string path, string sourceUri) =>
            _paths[(provider, externalId)] = (path, sourceUri);
    }

    private sealed class Provider(
        ProviderKind provider,
        string externalId,
        string path,
        int delayMs = 0) : IGameMediaProvider
    {
        public int ResolveCalls { get; private set; }

        public bool CanResolve(GameMediaIdentity identity) =>
            identity.Provider == provider && identity.ProviderGameId == externalId;

        public Task<GameMediaPayload?> ResolveAsync(
            GameMediaIdentity identity,
            GameMediaAssetType assetType,
            CancellationToken cancellationToken)
        {
            ResolveCalls++;
            return ResolveCoreAsync(assetType, cancellationToken);
        }

        private async Task<GameMediaPayload?> ResolveCoreAsync(
            GameMediaAssetType assetType,
            CancellationToken cancellationToken)
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken);
            }

            return new GameMediaPayload(
                    assetType,
                    path,
                    externalId,
                    [1],
                    "image/png",
                    new Uri($"https://example.test/{path}"));
        }
    }
}
