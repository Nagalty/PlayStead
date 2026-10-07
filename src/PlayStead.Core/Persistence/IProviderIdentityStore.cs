using PlayStead.Core.Identity;
using PlayStead.Core.Library;

namespace PlayStead.Core.Persistence;

public interface IProviderIdentityStore
{
    Task<IReadOnlyList<GameProviderIdentity>> GetByGameIdAsync(
        GameId gameId,
        CancellationToken cancellationToken);

    Task AssociateAsync(
        GameProviderIdentity identity,
        CancellationToken cancellationToken);

    Task<bool> RemoveAsync(
        GameId gameId,
        ProviderKind provider,
        string externalId,
        CancellationToken cancellationToken);
}
