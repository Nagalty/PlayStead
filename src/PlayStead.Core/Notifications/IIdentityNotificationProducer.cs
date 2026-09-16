using PlayStead.Core.Identity;
using PlayStead.Core.Library;

namespace PlayStead.Core.Notifications;

public interface IIdentityNotificationProducer
{
    Task PublishForResolutionAsync(
        GameId gameId,
        IdentityResolutionResult result,
        CancellationToken cancellationToken);
}
