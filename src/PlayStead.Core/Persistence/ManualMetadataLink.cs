using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Media;

namespace PlayStead.Core.Persistence;

public sealed record ManualMetadataLink(
    GameId ManualGameId,
    CatalogContentId CanonicalCatalogId,
    MediaSourceIdentity? MediaSource,
    DateTimeOffset MatchedAtUtc);

public interface IManualMetadataLinkStore
{
    Task<ManualMetadataLink?> GetAsync(GameId gameId, CancellationToken cancellationToken);
    Task UpsertAsync(ManualMetadataLink link, CancellationToken cancellationToken);
    Task RemoveAsync(GameId gameId, CancellationToken cancellationToken);
    ManualMetadataLink? TryGetCached(GameId gameId);
}
