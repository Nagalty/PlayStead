using PlayStead.Core.Persistence;

namespace PlayStead.Core.Identity;

public sealed class GameIdentityResolver : IGameIdentityResolver
{
    private readonly ICanonicalCatalogStore _catalogStore;

    public GameIdentityResolver(
        ICanonicalCatalogStore catalogStore)
    {
        ArgumentNullException.ThrowIfNull(catalogStore);
        _catalogStore = catalogStore;
    }

    public async Task<IdentityResolutionResult> ResolveAsync(
        GameIdentityObservation observation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var content = await _catalogStore.FindByProviderRefAsync(
            observation.Provider,
            observation.ExternalId,
            cancellationToken);

        if (content is not null)
        {
            return new IdentityResolutionResult(
                IdentityResolutionState.MatchConfirmed,
                content.Id,
                new IdentityResolutionEvidence(
                    IdentityResolutionEvidenceKind.ExactProviderRef,
                    observation.Provider,
                    observation.ExternalId,
                    content.Id));
        }

        return new IdentityResolutionResult(
            IdentityResolutionState.New,
            CandidateContentId: null,
            new IdentityResolutionEvidence(
                IdentityResolutionEvidenceKind.NoExactProviderRefMatch,
                observation.Provider,
                observation.ExternalId,
                MatchedContentId: null));
    }
}
