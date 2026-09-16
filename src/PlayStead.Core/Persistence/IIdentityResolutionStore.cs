using PlayStead.Core.Identity;
using PlayStead.Core.Library;

namespace PlayStead.Core.Persistence;

public interface IIdentityResolutionStore
{
    Task<GameIdentityResolution?> GetAsync(
        GameId gameId,
        CancellationToken cancellationToken);

    Task<GameIdentityResolution> GetOrCreateProvisionalAsync(
        GameId gameId,
        IdentityResolutionEvidence evidence,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);

    Task UpsertAsync(
        GameIdentityResolution resolution,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GameIdentityResolution>> ListAsync(
        CancellationToken cancellationToken);
}
