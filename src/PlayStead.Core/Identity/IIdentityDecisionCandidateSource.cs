using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public interface IIdentityDecisionCandidateSource
{
    Task<IReadOnlyList<IdentityDecisionCandidate>> GetCandidatesAsync(GameId gameId, CancellationToken cancellationToken);
}
