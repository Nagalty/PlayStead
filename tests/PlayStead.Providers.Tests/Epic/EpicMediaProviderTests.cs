using PlayStead.Core.Media;
using PlayStead.Core.Library;
using PlayStead.Core.Catalog;
using PlayStead.Core.Persistence;
using System.Net;
using PlayStead.Providers.Epic;

namespace PlayStead.Providers.Tests.Epic;

public sealed class EpicMediaProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-Epic-Media-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Resolves_tall_cover_and_wide_hero_deterministically()
    {
        Directory.CreateDirectory(_root);
        var id = "catalog-hell";
        await File.WriteAllBytesAsync(Path.Combine(_root, $"{id}_DieselGameBoxTall.jpg"), [1, 2, 3]);
        await File.WriteAllBytesAsync(Path.Combine(_root, $"{id}_DieselGameBox.jpg"), [4, 5, 6]);
        var provider = new EpicMediaProvider(new EpicLocalMediaLocator([_root]));
        var identity = new GameMediaIdentity(ProviderKind.Epic, id, "Hell Let Loose");

        var cover = await provider.ResolveAsync(identity, GameMediaAssetType.Cover, CancellationToken.None);
        var hero = await provider.ResolveAsync(identity, GameMediaAssetType.Hero, CancellationToken.None);

        Assert.Equal("epic-local", cover!.Source);
        Assert.EndsWith("DieselGameBoxTall.jpg", cover.SourceUri!.LocalPath, StringComparison.Ordinal);
        Assert.EndsWith("DieselGameBox.jpg", hero!.SourceUri!.LocalPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_non_epic_and_returns_null_for_missing_cache()
    {
        Directory.CreateDirectory(_root);
        var provider = new EpicMediaProvider(new EpicLocalMediaLocator([_root]));
        var epic = new GameMediaIdentity(ProviderKind.Epic, "missing", "Game");
        var steam = new GameMediaIdentity(ProviderKind.Steam, "123", "Game");

        Assert.True(provider.CanResolve(epic));
        Assert.False(provider.CanResolve(steam));
        Assert.Null(await provider.ResolveAsync(epic, GameMediaAssetType.Cover, CancellationToken.None));
    }

    [Fact]
    public async Task Uses_canonical_media_only_after_local_cache_miss()
    {
        var catalog = new FakeCatalogStore(new CatalogContent(
            new CatalogContentId(Guid.NewGuid()), PlaySteadPublicId.Parse("PlayStead-000001"), CatalogContentKind.Game,
            "Hell Let Loose", "HELL LET LOOSE", null, null, null, CatalogContentStatus.Active, null, [],
            new CatalogMedia("https://cdn.example.test/cover.jpg", "https://cdn.example.test/hero.jpg", 600, 900, 1920, 1080, "igdb")), CatalogProviderKind.Epic, "581c8d4fd9574884bff66cbdbaa42def");
        using var client = new HttpClient(new StubHandler());
        var provider = new EpicMediaProvider(new EpicLocalMediaLocator([_root]), catalog, client);
        var identity = new GameMediaIdentity(ProviderKind.Epic, "581c8d4fd9574884bff66cbdbaa42def", "Hell Let Loose");
        var result = await provider.ResolveAsync(identity, GameMediaAssetType.Cover, CancellationToken.None);
        Assert.Equal("epic-canonical", result?.Source);
        Assert.Equal("image/jpeg", result?.ContentType);
        Assert.Equal([1, 2, 3], result?.Content);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") } }
            });
    }

    private sealed class FakeCatalogStore(CatalogContent content, CatalogProviderKind provider, string externalId) : ICanonicalCatalogStore
    {
        public Task<CatalogMetadata> GetMetadataAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CatalogContent?> GetByIdAsync(CatalogContentId contentId, CancellationToken cancellationToken) => Task.FromResult<CatalogContent?>(null);
        public Task<CatalogContent?> GetByPublicIdAsync(PlaySteadPublicId publicId, CancellationToken cancellationToken) => Task.FromResult<CatalogContent?>(null);
        public Task<CatalogContent?> FindByProviderRefAsync(CatalogProviderKind p, string id, CancellationToken cancellationToken) => Task.FromResult(p == provider && id == externalId ? content : null);
        public Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(CatalogContentId contentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogProviderRef>>([]);
        public Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(CatalogContentId contentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogAlias>>([]);
        public Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(CatalogContentId sourceContentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogContentRelation>>([]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
