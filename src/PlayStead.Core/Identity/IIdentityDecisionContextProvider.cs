using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public interface IIdentityDecisionContextProvider
{
    Task<IdentityDecisionContext?> GetAsync(GameId gameId, CancellationToken cancellationToken);
}
