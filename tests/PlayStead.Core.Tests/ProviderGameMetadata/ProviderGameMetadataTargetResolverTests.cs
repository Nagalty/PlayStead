using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.ProviderGameMetadata;

namespace PlayStead.Core.Tests.ProviderGameMetadata;

public sealed class ProviderGameMetadataTargetResolverTests
{
    [Fact]
    public async Task Native_steam_installation_produces_native_target()
    {
        var game = GameId.New();
        var result = await ResolveAsync(
            Installation(game, ProviderKind.Steam, "123"),
            links: []);

        var target = Assert.Single(result);
        Assert.Equal(game, target.TargetGameId);
        Assert.Equal(ProviderKind.Steam, target.SourceProvider);
        Assert.Equal("123", target.SourceExternalId);
        Assert.Equal(ProviderGameMetadataTargetOrigin.Native, target.Origin);
    }

    [Fact]
    public async Task Manual_steam_link_produces_manual_bridge_target_without_changing_installation()
    {
        var game = GameId.New();
        var installation = Installation(game, ProviderKind.Manual, "manual:game");
        var result = await ResolveAsync(
            installation,
            [new ManualMetadataLink(game, CatalogContentId.New(), new(ProviderKind.Steam, "456"), DateTimeOffset.UtcNow)]);

        var target = Assert.Single(result);
        Assert.Equal(game, target.TargetGameId);
        Assert.Equal(ProviderKind.Steam, target.SourceProvider);
        Assert.Equal("456", target.SourceExternalId);
        Assert.Equal(ProviderGameMetadataTargetOrigin.ManualBridge, target.Origin);
        Assert.Equal(ProviderKind.Manual, installation.Provider);
        Assert.Equal("manual:game", installation.ExternalId);
    }

    [Fact]
    public async Task Manual_without_supported_link_produces_no_target()
    {
        var game = GameId.New();
        var installations = new[]
        {
            Installation(game, ProviderKind.Manual, "manual:none"),
            Installation(GameId.New(), ProviderKind.Manual, "manual:null"),
            Installation(GameId.New(), ProviderKind.Manual, "manual:epic"),
            Installation(GameId.New(), ProviderKind.Manual, "manual:invalid")
        };
        var links = new[]
        {
            new ManualMetadataLink(installations[1].GameId, CatalogContentId.New(), null, DateTimeOffset.UtcNow),
            new ManualMetadataLink(installations[2].GameId, CatalogContentId.New(), new(ProviderKind.Epic, "epic-id"), DateTimeOffset.UtcNow),
            new ManualMetadataLink(installations[3].GameId, CatalogContentId.New(), new(ProviderKind.Steam, "not-an-appid"), DateTimeOffset.UtcNow)
        };

        var result = await ResolveAsync(installations, links);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Duplicate_installations_are_deduplicated_but_shared_app_ids_keep_distinct_game_targets()
    {
        var first = GameId.New();
        var second = GameId.New();
        var installations = new[]
        {
            Installation(first, ProviderKind.Manual, "manual:first"),
            Installation(first, ProviderKind.Manual, "manual:first-duplicate"),
            Installation(second, ProviderKind.Manual, "manual:second"),
            Installation(GameId.New(), ProviderKind.Steam, "789")
        };
        var links = new[]
        {
            new ManualMetadataLink(first, CatalogContentId.New(), new(ProviderKind.Steam, "789"), DateTimeOffset.UtcNow),
            new ManualMetadataLink(second, CatalogContentId.New(), new(ProviderKind.Steam, "789"), DateTimeOffset.UtcNow)
        };

        var result = await ResolveAsync(installations, links);

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result.Count(x => x.SourceExternalId == "789" && x.Origin == ProviderGameMetadataTargetOrigin.ManualBridge));
        Assert.Contains(result, x => x.TargetGameId == first && x.Origin == ProviderGameMetadataTargetOrigin.ManualBridge);
        Assert.Contains(result, x => x.TargetGameId == second && x.Origin == ProviderGameMetadataTargetOrigin.ManualBridge);
        Assert.Contains(result, x => x.TargetGameId == installations[3].GameId && x.Origin == ProviderGameMetadataTargetOrigin.Native);
    }

    [Fact]
    public async Task Resolver_propagates_cancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ResolveAsync(Installation(GameId.New(), ProviderKind.Manual, "manual:cancel"), [], cancellation.Token));
    }

    [Fact]
    public async Task Manual_bridge_target_is_current_only_for_the_linked_app_id()
    {
        var game = GameId.New();
        var store = new MemoryLinkStore([
            new ManualMetadataLink(game, CatalogContentId.New(), new(ProviderKind.Steam, "B"), DateTimeOffset.UtcNow)]);
        var resolver = new ProviderGameMetadataTargetResolver(store);

        Assert.False(await resolver.IsCurrentAsync(
            new ProviderGameMetadataTarget(game, ProviderKind.Steam, "A", ProviderGameMetadataTargetOrigin.ManualBridge),
            CancellationToken.None));
        Assert.True(await resolver.IsCurrentAsync(
            new ProviderGameMetadataTarget(game, ProviderKind.Steam, "B", ProviderGameMetadataTargetOrigin.ManualBridge),
            CancellationToken.None));
    }

    private static Task<IReadOnlyList<ProviderGameMetadataTarget>> ResolveAsync(
        GameInstallation installation,
        IReadOnlyList<ManualMetadataLink> links,
        CancellationToken cancellationToken = default) =>
        ResolveAsync([installation], links, cancellationToken);

    private static async Task<IReadOnlyList<ProviderGameMetadataTarget>> ResolveAsync(
        IReadOnlyList<GameInstallation> installations,
        IReadOnlyList<ManualMetadataLink> links,
        CancellationToken cancellationToken = default)
    {
        var store = new MemoryLinkStore(links);
        var resolver = new ProviderGameMetadataTargetResolver(store);
        var snapshot = new LibrarySnapshot(
            installations.Select(x => new LogicalGame(x.GameId, x.GameId.ToString(), false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)).ToArray(),
            installations);
        return await resolver.ResolveAsync(snapshot, cancellationToken);
    }

    private static GameInstallation Installation(GameId gameId, ProviderKind provider, string externalId) =>
        new(InstallationId.New(), gameId, provider, externalId, @"C:\Games", null, true, true, DateTimeOffset.UtcNow);

    private sealed class MemoryLinkStore(IReadOnlyList<ManualMetadataLink> links) : IManualMetadataLinkStore
    {
        public Task<ManualMetadataLink?> GetAsync(GameId gameId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(links.FirstOrDefault(x => x.ManualGameId == gameId));
        }

        public Task UpsertAsync(ManualMetadataLink link, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RemoveAsync(GameId gameId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ManualMetadataLink? TryGetCached(GameId gameId) => links.FirstOrDefault(x => x.ManualGameId == gameId);
    }
}
