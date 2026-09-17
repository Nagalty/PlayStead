using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Identity;

public interface IIdentityDecisionApplicationService
{
    Task<IdentityDecisionContext?> GetContextAsync(GameId gameId, CancellationToken cancellationToken);
    Task ConfirmAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken);
    Task RejectAsync(GameId gameId, CatalogContentId catalogContentId, CancellationToken cancellationToken);
}
