using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public sealed class EmptyIdentityDecisionCandidateSource : IIdentityDecisionCandidateSource
{
    public Task<IReadOnlyList<IdentityDecisionCandidate>> GetCandidatesAsync(GameId gameId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<IdentityDecisionCandidate>>([]);
    }
}
