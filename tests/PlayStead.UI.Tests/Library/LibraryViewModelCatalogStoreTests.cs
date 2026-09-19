using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Core.Sessions;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using PlayStead.UI.Sessions;
using PlayStead.UI.Settings;
using PlayStead.UI.Steam;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryViewModelCatalogStoreTests
{
    [Fact]
    public async Task Production_style_constructor_forwards_catalog_store_to_game_detail_projection()
    {
        var gameId = GameId.New();
        var contentId = CatalogContentId.New();
        var snapshot = new LibrarySnapshot(
            [new LogicalGame(gameId, "Enshrouded", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, contentId)],
            [new GameInstallation(InstallationId.New(), gameId, ProviderKind.Steam, "1203620", @"C:\Steam\Enshrouded", null, true, true, DateTimeOffset.UtcNow)]);
        var content = new CatalogContent(
            contentId,
            PlaySteadPublicId.Parse("PlayStead-1203620"),
            CatalogContentKind.Game,
            "Enshrouded",
            "enshrouded",
            null,
            "Keen Games GmbH",
            "Keen Games GmbH",
            CatalogContentStatus.Active,
            null);
        var catalogStore = new FakeCatalogStore(content);
        var libraryStore = new FakeLibraryStore(snapshot);
        var sessionMonitor = new SessionMonitor(
            new EmptySessionRuntime(),
            SessionMonitorOptions.Default,
            (_, _) => Task.CompletedTask);
        var preferences = new UiPreferencesStore(Path.Combine(Path.GetTempPath(), $"playstead-preferences-{Guid.NewGuid():N}.json"));
        var viewModel = new LibraryViewModel(
            libraryStore,
            new EmptySteamReferenceRuntime(),
            sessionMonitor,
            preferences,
            new EmptyGameMediaResolver(),
            catalogStore);

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Same(catalogStore, viewModel.CanonicalCatalogStore);
        var game = Assert.Single(viewModel.Items);
        var detail = new GameDetailViewModel(
            game,
            launch: null,
            activity: null,
            heroPath: null,
            catalogStore: viewModel.CanonicalCatalogStore);

        await detail.LoadAsync(CancellationToken.None);

        Assert.Equal("Keen Games GmbH", detail.DeveloperDisplay);
        Assert.Equal("Keen Games GmbH", detail.PublisherDisplay);
        Assert.Null(detail.ReleaseDateDisplay);
        Assert.True(detail.HasGeneralInfo);
        Assert.DoesNotContain("Inconnu", detail.DeveloperDisplay ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Inconnu", detail.PublisherDisplay ?? string.Empty, StringComparison.Ordinal);
    }

    private sealed class FakeLibraryStore(LibrarySnapshot snapshot) : ILibraryStore
    {
        public Task ApplySourceScanAsync(SourceScanResult result, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class EmptySteamReferenceRuntime : ISteamReferenceRuntime
    {
        public SteamReferenceSnapshot Current => SteamReferenceSnapshot.Empty;
        public Task<SteamReferenceSnapshot> LoadCachedAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task<SteamReferenceSnapshot> RefreshStaleAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task<SteamReferenceSnapshot> RefreshAllAsync(CancellationToken cancellationToken) => Task.FromResult(Current);
    }

    private sealed class EmptySessionRuntime : ISessionRuntime
    {
        public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SessionRuntimeSnapshot(DateTimeOffset.UtcNow, []));

        public Task CorrectSessionAsync(SessionCorrectionRequest correction, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class EmptyGameMediaResolver : IGameMediaResolver
    {
        public string? TryGetCachedPath(GameMediaIdentity identity, GameMediaAssetType assetType) => null;
        public Task<string?> ResolveAndCacheAsync(GameMediaIdentity identity, GameMediaAssetType assetType, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FakeCatalogStore(CatalogContent content) : ICanonicalCatalogStore
    {
        public Task<CatalogContent?> GetByIdAsync(CatalogContentId id, CancellationToken cancellationToken) =>
            Task.FromResult<CatalogContent?>(id == content.Id ? content : null);

        public Task<CatalogMetadata> GetMetadataAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> GetByPublicIdAsync(PlaySteadPublicId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> FindByProviderRefAsync(CatalogProviderKind provider, string externalId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(CatalogContentId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(CatalogContentId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(CatalogContentId id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
