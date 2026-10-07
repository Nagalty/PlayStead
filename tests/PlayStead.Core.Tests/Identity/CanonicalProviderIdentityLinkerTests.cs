using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Tests.Identity;

public sealed class CanonicalProviderIdentityLinkerTests
{
    [Fact]
    public async Task Sync_links_deterministic_refs_idempotently_and_preserves_user_confirmed_identity()
    {
        var gameId = new GameId(Guid.NewGuid());
        var contentId = CatalogContentId.New();
        var catalog = new CatalogStore(
            new CatalogProviderRef(contentId, CatalogProviderKind.Steam, "3768760", null,
                CatalogProvenance.AdminConfirmed, CatalogConfidence.Deterministic, DateTimeOffset.UtcNow));
        var identities = new IdentityStore(
            new GameProviderIdentity(gameId, ProviderKind.Steam, "3768760",
                ProviderIdentitySource.UserConfirmed, CatalogConfidence.Deterministic,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var sut = new CanonicalProviderIdentityLinker(catalog, identities);

        await sut.SyncAsync(gameId, contentId, CancellationToken.None);
        await sut.SyncAsync(gameId, contentId, CancellationToken.None);

        var result = await identities.GetByGameIdAsync(gameId, CancellationToken.None);
        var identity = Assert.Single(result);
        Assert.Equal(ProviderIdentitySource.UserConfirmed, identity.Source);
        Assert.Equal("3768760", identity.ExternalId);
    }

    [Fact]
    public async Task RemoveDerivedAsync_removes_only_canonical_catalog_identities()
    {
        var gameId = new GameId(Guid.NewGuid());
        var identities = new IdentityStore(
            new GameProviderIdentity(gameId, ProviderKind.Steam, "3768760",
                ProviderIdentitySource.CanonicalCatalog, CatalogConfidence.Deterministic,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new GameProviderIdentity(gameId, ProviderKind.Epic, "epic-user",
                ProviderIdentitySource.UserConfirmed, CatalogConfidence.Deterministic,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var sut = new CanonicalProviderIdentityLinker(new CatalogStore(), identities);

        await sut.RemoveDerivedAsync(gameId, CancellationToken.None);

        var result = await identities.GetByGameIdAsync(gameId, CancellationToken.None);
        var remaining = Assert.Single(result);
        Assert.Equal(ProviderIdentitySource.UserConfirmed, remaining.Source);
    }

    private sealed class CatalogStore(params CatalogProviderRef[] refs) : ICanonicalCatalogStore
    {
        public Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(CatalogContentId contentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CatalogProviderRef>>(refs);
        public Task<CatalogMetadata> GetMetadataAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> GetByIdAsync(CatalogContentId contentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> GetByPublicIdAsync(PlaySteadPublicId publicId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CatalogContent?> FindByProviderRefAsync(CatalogProviderKind provider, string externalId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(CatalogContentId contentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(CatalogContentId sourceContentId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class IdentityStore(params GameProviderIdentity[] seed) : IProviderIdentityStore
    {
        private readonly List<GameProviderIdentity> _items = [.. seed];
        public Task<IReadOnlyList<GameProviderIdentity>> GetByGameIdAsync(GameId gameId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameProviderIdentity>>(_items.Where(x => x.GameId == gameId).ToArray());
        public Task AssociateAsync(GameProviderIdentity identity, CancellationToken cancellationToken)
        {
            var existing = _items.FindIndex(x => x.GameId == identity.GameId && x.Provider == identity.Provider && x.ExternalId == identity.ExternalId);
            if (existing < 0) _items.Add(identity);
            return Task.CompletedTask;
        }
        public Task<bool> RemoveAsync(GameId gameId, ProviderKind provider, string externalId, CancellationToken cancellationToken) =>
            Task.FromResult(_items.RemoveAll(x => x.GameId == gameId && x.Provider == provider && x.ExternalId == externalId) > 0);
    }
}
