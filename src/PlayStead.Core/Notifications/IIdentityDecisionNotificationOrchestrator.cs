using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Identity;

namespace PlayStead.Core.Notifications;

public interface IIdentityDecisionNotificationOrchestrator
{
    Task ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken);
    Task RejectAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken);
    Task<GameIdentityDecision?> GetActiveConfirmedAsync(GameId gameId, CancellationToken cancellationToken);
    Task RevokeConfirmedAsync(GameId gameId, CancellationToken cancellationToken);
}
