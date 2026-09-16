using PlayStead.Core.Library;

namespace PlayStead.Core.Persistence;

public interface ILibraryGameLookup
{
    Task<GameId?> FindGameIdByProviderRefAsync(
        ProviderKind provider,
        string externalId,
        CancellationToken cancellationToken);
}
