using PlayStead.Core.Identity;
using PlayStead.Core.Library;

namespace PlayStead.Core.Persistence;

public interface IIdentityDecisionStore
{
    Task<GameIdentityDecision?> GetActiveConfirmedAsync(GameId gameId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GameIdentityDecision>> ListActiveRejectedAsync(GameId gameId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GameIdentityDecision>> ListActiveAsync(GameId gameId, CancellationToken cancellationToken);

    Task InsertAsync(GameIdentityDecision decision, CancellationToken cancellationToken);

    Task RevokeAsync(IdentityDecisionId decisionId, DateTimeOffset revokedUtc, CancellationToken cancellationToken);
}
