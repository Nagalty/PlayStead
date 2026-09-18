using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailMetadataProjectionTests
{
    [Fact]
    public async Task Canonical_metadata_is_projected_without_network()
    {
        var contentId = CatalogContentId.New();
        var content = new CatalogContent(contentId, PlaySteadPublicId.Parse("PlayStead-123456"), CatalogContentKind.Game, "Game", "game", new DateOnly(2020, 4, 5), "Dev", "Pub", CatalogContentStatus.Active, null);
        var item = new LibraryItemViewModel(GameId.New(), "Game", ProviderKind.Steam, "Steam", @"C:\Games\Game", null, CanonicalContentId: contentId);
        var vm = new GameDetailViewModel(item, null, null, null, new FakeCatalogStore(content));

        await vm.LoadAsync(CancellationToken.None);

        Assert.Equal("Dev", vm.DeveloperDisplay);
        Assert.Equal("Pub", vm.PublisherDisplay);
        Assert.Equal("5 avril 2020", vm.ReleaseDateDisplay);
    }

    [Fact]
    public async Task Missing_canonical_metadata_remains_unavailable()
    {
        var item = new LibraryItemViewModel(GameId.New(), "Game", ProviderKind.Steam, "Steam", @"C:\Games\Game", null);
        var vm = new GameDetailViewModel(item, null, null, null, new FakeCatalogStore(null));
        await vm.LoadAsync(CancellationToken.None);
        Assert.Null(vm.DeveloperDisplay);
        Assert.Null(vm.PublisherDisplay);
        Assert.Null(vm.ReleaseDateDisplay);
    }

    private sealed class FakeCatalogStore(CatalogContent? content) : ICanonicalCatalogStore
    {
        public Task<CatalogContent?> GetByIdAsync(CatalogContentId id, CancellationToken cancellationToken) => Task.FromResult(content);
        public Task<CatalogMetadata> GetMetadataAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> GetByPublicIdAsync(PlaySteadPublicId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> FindByProviderRefAsync(CatalogProviderKind provider, string externalId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(CatalogContentId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(CatalogContentId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(CatalogContentId id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}