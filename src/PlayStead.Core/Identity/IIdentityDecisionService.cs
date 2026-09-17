using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public interface IIdentityDecisionService
{
    Task<GameIdentityDecision> ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, DateTimeOffset decidedUtc, CancellationToken cancellationToken);

    Task<GameIdentityDecision> RejectAsync(GameId gameId, CatalogContentId catalogContentId, DateTimeOffset decidedUtc, CancellationToken cancellationToken);

    Task<GameIdentityDecision?> RevokeConfirmedAsync(GameId gameId, DateTimeOffset revokedUtc, CancellationToken cancellationToken);
}
