using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Identity;

public sealed class CanonicalProviderIdentityLinker
{
    private readonly ICanonicalCatalogStore _catalogStore;
    private readonly IProviderIdentityStore _identityStore;

    public CanonicalProviderIdentityLinker(
        ICanonicalCatalogStore catalogStore,
        IProviderIdentityStore identityStore)
    {
        _catalogStore = catalogStore ?? throw new ArgumentNullException(nameof(catalogStore));
        _identityStore = identityStore ?? throw new ArgumentNullException(nameof(identityStore));
    }

    public async Task SyncAsync(
        GameId gameId,
        CatalogContentId contentId,
        CancellationToken cancellationToken)
    {
        var existing = await _identityStore.GetByGameIdAsync(gameId, cancellationToken);
        var refs = await _catalogStore.GetProviderRefsAsync(contentId, cancellationToken);
        foreach (var reference in refs.Where(IsSupportedDeterministicReference))
        {
            var provider = ToProvider(reference.Provider);
            var current = existing.FirstOrDefault(identity =>
                identity.Provider == provider &&
                string.Equals(identity.ExternalId, reference.ExternalId, StringComparison.OrdinalIgnoreCase));
            if (current is not null && current.Source == ProviderIdentitySource.UserConfirmed)
                continue;

            try
            {
                await _identityStore.AssociateAsync(
                    new GameProviderIdentity(
                        gameId,
                        provider,
                        reference.ExternalId,
                        ProviderIdentitySource.CanonicalCatalog,
                        CatalogConfidence.Deterministic,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow),
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                // A provider reference owned by another game remains untouched.
            }
        }
    }

    public async Task RemoveDerivedAsync(GameId gameId, CancellationToken cancellationToken)
    {
        var identities = await _identityStore.GetByGameIdAsync(gameId, cancellationToken);
        foreach (var identity in identities.Where(identity =>
                     identity.Source == ProviderIdentitySource.CanonicalCatalog &&
                     identity.Confidence == CatalogConfidence.Deterministic))
        {
            await _identityStore.RemoveAsync(gameId, identity.Provider, identity.ExternalId, cancellationToken);
        }
    }

    private static bool IsSupportedDeterministicReference(CatalogProviderRef reference) =>
        reference.Confidence == CatalogConfidence.Deterministic &&
        reference.Provider is CatalogProviderKind.Steam or CatalogProviderKind.Epic or CatalogProviderKind.Gog;

    private static ProviderKind ToProvider(CatalogProviderKind provider) =>
        provider switch
        {
            CatalogProviderKind.Steam => ProviderKind.Steam,
            CatalogProviderKind.Epic => ProviderKind.Epic,
            CatalogProviderKind.Gog => ProviderKind.Gog,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
}
