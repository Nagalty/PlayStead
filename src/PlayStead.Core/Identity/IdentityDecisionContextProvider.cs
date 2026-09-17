using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Identity;

public sealed class IdentityDecisionContextProvider : IIdentityDecisionContextProvider
{
    private readonly IIdentityDecisionCandidateSource _source;
    private readonly IIdentityDecisionStore _decisions;
    public IdentityDecisionContextProvider(IIdentityDecisionCandidateSource source, IIdentityDecisionStore decisions) { ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(decisions); _source = source; _decisions = decisions; }
    public async Task<IdentityDecisionContext?> GetAsync(GameId gameId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await _decisions.GetActiveConfirmedAsync(gameId, cancellationToken) is not null) return null;
        var rejected = await _decisions.ListActiveRejectedAsync(gameId, cancellationToken);
        var rejectedIds = rejected.Select(x => x.CatalogContentId).ToHashSet();
        var candidates = (await _source.GetCandidatesAsync(gameId, cancellationToken)).Where(x => !rejectedIds.Contains(x.CatalogContentId)).ToArray();
        var state = candidates.Length switch { 0 => IdentityResolutionState.New, 1 => IdentityResolutionState.MatchProbable, _ => IdentityResolutionState.Ambiguous };
        return IdentityDecisionContext.Create(gameId, state, candidates);
    }
}
