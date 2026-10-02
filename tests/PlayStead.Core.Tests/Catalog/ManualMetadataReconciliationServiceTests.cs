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
    public async Task Reconcile_removes_stale_steam_metadata_when_manual_link_loses_steam_source_and_signals_change()
    {
        var gameId = GameId.New();
        var installation = new GameInstallation(
            InstallationId.New(), gameId, ProviderKind.Manual, "manual:007",
            @"H:\\007 First Light", 123, true, true, DateTimeOffset.UtcNow,
            InstallationContentKind.Game,
            @"H:\\007 First Light\\Retail\\007FirstLight.exe",
            @"H:\\007 First Light\\Retail", null, @"H:\\007 First Light");
        var content = new CatalogContent(
            CatalogContentId.New(), PlaySteadPublicId.Parse("PlayStead-123456"), CatalogContentKind.Game,
            "007 First Light", "007 first light", null, "Developer", "Publisher",
            CatalogContentStatus.Active, null);
        var catalog = new FakeCatalogStore(content, []);
        var links = new FakeLinkStore
        {
            Link = new(gameId, content.Id, new MediaSourceIdentity(ProviderKind.Steam, "3768760"), DateTimeOffset.UtcNow)
        };
        var metadata = new FakeMetadataStore();
        await metadata.UpsertAsync(ProviderMetadata.Create(gameId, ProviderKind.Steam, "3768760", DateTimeOffset.UtcNow), CancellationToken.None);
        var service = new ManualMetadataReconciliationService(
            new FakeLibraryStore(
                new LogicalGame(gameId, "007 First Light", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                installation), catalog, links, metadata);
        var changed = 0;
        service.Changed += (_, _) => changed++;

        var result = await service.ReconcileAsync(CancellationToken.None);

        Assert.Equal(1, result.Matched);
        Assert.Null(await metadata.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None));
        Assert.NotNull(await metadata.GetAsync(gameId, ProviderKind.Manual, CancellationToken.None));
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Reconcile_removes_old_steam_metadata_when_manual_link_reassigns_app_id()
    {
        var (service, gameId, links, metadata, content) = CreateReconciliation(
            [new CatalogProviderRef(CatalogContentId.New(), CatalogProviderKind.Steam, "B", null, CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic, DateTimeOffset.UtcNow)],
            new MediaSourceIdentity(ProviderKind.Steam, "A"));
        await metadata.UpsertAsync(ProviderMetadata.Create(gameId, ProviderKind.Steam, "A", DateTimeOffset.UtcNow), CancellationToken.None);

        await service.ReconcileAsync(CancellationToken.None);

        Assert.Equal(new MediaSourceIdentity(ProviderKind.Steam, "B"), links.Link!.MediaSource);
        Assert.Null(await metadata.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None));
        Assert.Equal(content.Id, links.Link.CanonicalCatalogId);
    }

    [Fact]
    public async Task Reconcile_removes_old_steam_metadata_when_manual_link_becomes_non_steam()
    {
        var (service, gameId, links, metadata, _) = CreateReconciliation(
            [new CatalogProviderRef(CatalogContentId.New(), CatalogProviderKind.Epic, "epic", null, CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic, DateTimeOffset.UtcNow)],
            new MediaSourceIdentity(ProviderKind.Steam, "A"));
        await metadata.UpsertAsync(ProviderMetadata.Create(gameId, ProviderKind.Steam, "A", DateTimeOffset.UtcNow), CancellationToken.None);

        await service.ReconcileAsync(CancellationToken.None);

        Assert.Null(links.Link!.MediaSource);
        Assert.Null(await metadata.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None));
    }

    private static (ManualMetadataReconciliationService Service, GameId GameId, FakeLinkStore Links, FakeMetadataStore Metadata, CatalogContent Content) CreateReconciliation(
        IReadOnlyList<CatalogProviderRef> refs,
        MediaSourceIdentity oldSource)
    {
        var gameId = GameId.New();
        var installation = new GameInstallation(InstallationId.New(), gameId, ProviderKind.Manual, "manual:007", @"H:\\007 First Light", 123, true, true, DateTimeOffset.UtcNow, InstallationContentKind.Game, @"H:\\007 First Light\\Retail\\007FirstLight.exe", @"H:\\007 First Light\\Retail", null, @"H:\\007 First Light");
        var content = new CatalogContent(CatalogContentId.New(), PlaySteadPublicId.Parse("PlayStead-123456"), CatalogContentKind.Game, "007 First Light", "007 first light", null, "Developer", "Publisher", CatalogContentStatus.Active, null);
        var catalogRefs = refs.Select(x => x with { ContentId = content.Id }).ToArray();
        var links = new FakeLinkStore { Link = new(gameId, content.Id, oldSource, DateTimeOffset.UtcNow) };
        var metadata = new FakeMetadataStore();
        return (new ManualMetadataReconciliationService(new FakeLibraryStore(new LogicalGame(gameId, "007 First Light", false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), installation), new FakeCatalogStore(content, catalogRefs), links, metadata), gameId, links, metadata, content);
    }

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
        var catalog = new FakeCatalogStore(content, [new CatalogProviderRef(
            content.Id, CatalogProviderKind.Steam, "3768760", null,
            CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic, DateTimeOffset.UtcNow),
            new CatalogProviderRef(content.Id, CatalogProviderKind.Epic, "c04cf17392964f2594620101490bdb21", null,
                CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic, DateTimeOffset.UtcNow)]);
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
        Assert.Equal("007 FIRST LIGHT", catalog.LastNormalizedTitle);
        Assert.Contains(catalog.ProviderRefs, x => x.Provider == CatalogProviderKind.Epic);
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

    private sealed class FakeCatalogStore(CatalogContent content, IReadOnlyList<CatalogProviderRef> providerRefs) : ICanonicalCatalogStore
    {
        public string? LastNormalizedTitle { get; private set; }
        public IReadOnlyList<CatalogProviderRef> ProviderRefs => providerRefs;
        public Task<CatalogMetadata> GetMetadataAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> GetByIdAsync(CatalogContentId contentId, CancellationToken cancellationToken) => Task.FromResult<CatalogContent?>(contentId == content.Id ? content : null);
        public Task<CatalogContent?> GetByPublicIdAsync(PlaySteadPublicId publicId, CancellationToken cancellationToken) => Task.FromResult<CatalogContent?>(null);
        public Task<CatalogContent?> FindByProviderRefAsync(CatalogProviderKind provider, string externalId, CancellationToken cancellationToken) => Task.FromResult<CatalogContent?>(null);
        public Task<IReadOnlyList<CatalogContent>> FindByNormalizedTitleAsync(string normalizedTitle, CancellationToken cancellationToken) { LastNormalizedTitle = normalizedTitle; return Task.FromResult<IReadOnlyList<CatalogContent>>([content]); }
        public Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(CatalogContentId contentId, CancellationToken cancellationToken) => Task.FromResult(providerRefs);
        public Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(CatalogContentId contentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogAlias>>([]);
        public Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(CatalogContentId sourceContentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogContentRelation>>([]);
    }

    private sealed class FakeLinkStore : IManualMetadataLinkStore
    {
        public ManualMetadataLink? Link { get; set; }
        public Task<ManualMetadataLink?> GetAsync(GameId gameId, CancellationToken cancellationToken) => Task.FromResult(Link);
        public Task UpsertAsync(ManualMetadataLink link, CancellationToken cancellationToken) { Link = link; return Task.CompletedTask; }
        public Task RemoveAsync(GameId gameId, CancellationToken cancellationToken) { Link = null; return Task.CompletedTask; }
        public ManualMetadataLink? TryGetCached(GameId gameId) => Link;
    }

    private sealed class FakeMetadataStore : IProviderGameMetadataStore
    {
        private readonly List<ProviderMetadata> _values = [];
        public ProviderMetadata? Value => _values.FirstOrDefault(x => x.Provider == ProviderKind.Manual);
        public Task<IReadOnlyList<ProviderMetadata>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProviderMetadata>>(_values);
        public Task<ProviderMetadata?> GetAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) => Task.FromResult<ProviderMetadata?>(_values.FirstOrDefault(x => x.GameId == gameId && x.Provider == provider));
        public Task UpsertAsync(ProviderMetadata metadata, CancellationToken cancellationToken)
        {
            _values.RemoveAll(x => x.GameId == metadata.GameId && x.Provider == metadata.Provider);
            _values.Add(metadata);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken)
        {
            _values.RemoveAll(x => x.GameId == gameId && x.Provider == provider);
            return Task.CompletedTask;
        }
    }
}
