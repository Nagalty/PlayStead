using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public interface IIdentityDecisionContextGateway
{
    Task<IdentityDecisionContext?> GetAsync(GameId gameId, CancellationToken cancellationToken);
}
