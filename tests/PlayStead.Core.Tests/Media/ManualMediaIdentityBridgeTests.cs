using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Media;

public sealed class ManualMediaIdentityBridgeTests
{
    [Fact]
    public async Task Manual_007_catalog_link_bridges_steam_reference_without_mutating_installation_identity()
    {
        var game = GameId.New();
        var installation = new GameInstallation(
            InstallationId.New(), game, ProviderKind.Manual, $"manual:{game}", @"H:\007 First Light",
            57_200_000_000, true, true, DateTimeOffset.UtcNow, InstallationContentKind.Game,
            @"H:\007 First Light\Retail\007FirstLight.exe", @"H:\007 First Light\Retail", null, @"H:\007 First Light");
        var links = new LinkStore(new ManualMetadataLink(game, CatalogContentId.New(), new(ProviderKind.Steam, "3768760"), DateTimeOffset.UtcNow));
        var inner = new RecordingResolver();
        var bridge = new ManualMediaIdentityBridge(inner, links);

        await bridge.ResolveAndCacheAsync(new GameMediaIdentity(ProviderKind.Manual, $"manual:{game}", "007 First Light"), GameMediaAssetType.Cover, CancellationToken.None);

        Assert.Equal(ProviderKind.Manual, installation.Provider);
        Assert.Equal($"manual:{game}", installation.ExternalId);
        Assert.Equal(@"H:\007 First Light", installation.InstallRootPath);
        Assert.Equal(@"H:\007 First Light\Retail", installation.WorkingDirectory);
        Assert.Equal(ProviderKind.Steam, inner.LastIdentity?.Provider);
        Assert.Equal("3768760", inner.LastIdentity?.ProviderGameId);
    }

    [Fact]
    public async Task Manual_identity_resolves_through_persisted_steam_identity_without_changing_provider()
    {
        var game = GameId.New();
        var links = new LinkStore(new ManualMetadataLink(
            game,
            CatalogContentId.New(),
            new MediaSourceIdentity(ProviderKind.Steam, "123"),
            DateTimeOffset.UtcNow));
        var inner = new RecordingResolver();
        var bridge = new ManualMediaIdentityBridge(inner, links);
        var identity = new GameMediaIdentity(ProviderKind.Manual, $"manual:{game}", "Game");

        await bridge.ResolveAndCacheAsync(identity, GameMediaAssetType.Cover, CancellationToken.None);

        Assert.Equal(ProviderKind.Manual, identity.Provider);
        Assert.Equal("123", inner.LastIdentity?.ProviderGameId);
        Assert.Equal(ProviderKind.Steam, inner.LastIdentity?.Provider);
    }

    [Fact]
    public async Task Missing_media_reference_falls_back_without_calling_inner_resolver()
    {
        var game = GameId.New();
        var links = new LinkStore(new ManualMetadataLink(game, CatalogContentId.New(), null, DateTimeOffset.UtcNow));
        var inner = new RecordingResolver();
        var bridge = new ManualMediaIdentityBridge(inner, links);

        var path = await bridge.ResolveAndCacheAsync(
            new GameMediaIdentity(ProviderKind.Manual, $"manual:{game}", "Game"),
            GameMediaAssetType.Cover,
            CancellationToken.None);

        Assert.Null(path);
        Assert.Null(inner.LastIdentity);
    }

    [Fact]
    public void Cached_manual_identity_reaches_provider_cache_with_translated_identity()
    {
        var game = GameId.New();
        var links = new LinkStore(new ManualMetadataLink(game, CatalogContentId.New(), new(ProviderKind.Steam, "123"), DateTimeOffset.UtcNow));
        var inner = new RecordingResolver { CachedPath = @"C:\Media\steam\123\hero.jpg" };
        var bridge = new ManualMediaIdentityBridge(inner, links);

        var path = bridge.TryGetCachedPath(new GameMediaIdentity(ProviderKind.Manual, $"manual:{game}", "Game"), GameMediaAssetType.Hero);

        Assert.Equal(inner.CachedPath, path);
        Assert.Equal(ProviderKind.Steam, inner.LastIdentity?.Provider);
        Assert.Equal("123", inner.LastIdentity?.ProviderGameId);
    }

    private sealed class LinkStore(ManualMetadataLink link) : IManualMetadataLinkStore
    {
        public Task<ManualMetadataLink?> GetAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult<ManualMetadataLink?>(link);
        public Task UpsertAsync(ManualMetadataLink link, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveAsync(GameId gameId, CancellationToken cancellationToken) => Task.CompletedTask;
        public ManualMetadataLink? TryGetCached(GameId gameId) => link;
    }

    private sealed class RecordingResolver : IGameMediaResolver
    {
        public GameMediaIdentity? LastIdentity { get; private set; }
        public string? CachedPath { get; init; }
        public string? TryGetCachedPath(GameMediaIdentity identity, GameMediaAssetType assetType)
        {
            LastIdentity = identity;
            return CachedPath;
        }
        public Task<string?> ResolveAndCacheAsync(GameMediaIdentity identity, GameMediaAssetType assetType, CancellationToken cancellationToken)
        {
            LastIdentity = identity;
            return Task.FromResult<string?>(null);
        }
    }
}
