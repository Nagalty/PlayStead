using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Core.ProviderGameMetadata;
using PlayStead.Core.Scanning;
using ProviderMetadata = PlayStead.Core.ProviderGameMetadata.ProviderGameMetadata;

namespace PlayStead.Core.Tests.Catalog;

public sealed class ManualMetadataReconciliationServiceTests
{
    [Fact]
    public async Task Reconcile_matches_007_without_changing_manual_identity_or_paths_and_is_idempotent()
    {
        var gameId = GameId.New();
        var installation = new GameInstallation(
            InstallationId.New(), gameId, ProviderKind.Manual, "manual:007",
            @"H:\007 First Light", 123, true, true, DateTimeOffset.UtcNow,
            InstallationContentKind.Game,
            @"H:\007 First Light\Retail\007FirstLight.exe",
            @"H:\007 First Light\Retail", null, @"H:\007 First Light");
        var content = new CatalogContent(
            CatalogContentId.New(), PlaySteadPublicId.Parse("PlayStead-123456"), CatalogContentKind.Game,
            "007 First Light", "007 first light", null, "Developer", "Publisher",
            CatalogContentStatus.Active, null);
        var catalog = new FakeCatalogStore(content, new CatalogProviderRef(
            content.Id, CatalogProviderKind.Steam, "3768760", null,
            CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic, DateTimeOffset.UtcNow));
        var links = new FakeLinkStore();
        var metadata = new FakeMetadataStore();
        var service = new ManualMetadataReconciliationService(
            new FakeLibraryStore(
                new LogicalGame(gameId, "007 First Light", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                installation), catalog, links, metadata);

        var first = await service.ReconcileAsync(CancellationToken.None);
        var second = await service.ReconcileAsync(CancellationToken.None);

        var link = links.Link;
        Assert.NotNull(link);
        Assert.Equal(1, first.Matched);
        Assert.Equal(1, first.Updated);
        Assert.Equal(1, second.Matched);
        Assert.Equal(0, second.Updated);
        Assert.Equal(content.Id, link!.CanonicalCatalogId);
        Assert.Equal(new MediaSourceIdentity(ProviderKind.Steam, "3768760"), link.MediaSource);
        Assert.Equal(ProviderKind.Manual, installation.Provider);
        Assert.Equal("manual:007", installation.ExternalId);
        Assert.Equal(@"H:\007 First Light", installation.InstallPath);
        Assert.Equal(@"H:\007 First Light\Retail", installation.WorkingDirectory);
        Assert.Equal(gameId, metadata.Value!.GameId);
        Assert.Equal(ProviderKind.Manual, metadata.Value.Provider);
    }

    private sealed class FakeLibraryStore(LogicalGame game, GameInstallation installation) : ILibraryStore
    {
        public Task ApplySourceScanAsync(SourceScanResult result, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<LibrarySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new LibrarySnapshot([game], [installation]));
    }

    private sealed class FakeCatalogStore(CatalogContent content, CatalogProviderRef providerRef) : ICanonicalCatalogStore
    {
        public Task<CatalogMetadata> GetMetadataAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> GetByIdAsync(CatalogContentId contentId, CancellationToken cancellationToken) => Task.FromResult<CatalogContent?>(contentId == content.Id ? content : null);
        public Task<CatalogContent?> GetByPublicIdAsync(PlaySteadPublicId publicId, CancellationToken cancellationToken) => Task.FromResult<CatalogContent?>(null);
        public Task<CatalogContent?> FindByProviderRefAsync(CatalogProviderKind provider, string externalId, CancellationToken cancellationToken) => Task.FromResult<CatalogContent?>(null);
        public Task<IReadOnlyList<CatalogContent>> FindByNormalizedTitleAsync(string normalizedTitle, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogContent>>([content]);
        public Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(CatalogContentId contentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogProviderRef>>([providerRef]);
        public Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(CatalogContentId contentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogAlias>>([]);
        public Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(CatalogContentId sourceContentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogContentRelation>>([]);
    }

    private sealed class FakeLinkStore : IManualMetadataLinkStore
    {
        public ManualMetadataLink? Link { get; private set; }
        public Task<ManualMetadataLink?> GetAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult(Link);
        public Task UpsertAsync(ManualMetadataLink link, CancellationToken cancellationToken) { Link = link; return Task.CompletedTask; }
        public Task RemoveAsync(GameId gameId, CancellationToken cancellationToken) { Link = null; return Task.CompletedTask; }
        public ManualMetadataLink? TryGetCached(GameId gameId) => Link;
    }

    private sealed class FakeMetadataStore : IProviderGameMetadataStore
    {
        public ProviderMetadata? Value { get; private set; }
        public Task<IReadOnlyList<ProviderMetadata>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProviderMetadata>>(Value is null ? [] : [Value]);
        public Task<ProviderMetadata?> GetAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) => Task.FromResult(Value);
        public Task UpsertAsync(ProviderMetadata metadata, CancellationToken cancellationToken) { Value = metadata; return Task.CompletedTask; }
    }
}
