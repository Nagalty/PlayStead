using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Notifications;

public interface IIdentityDecisionNotificationOrchestrator
{
    Task ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken);
    Task RejectAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken);
}
