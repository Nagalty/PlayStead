using PlayStead.Core.Catalog;
using PlayStead.Core.Library;

namespace PlayStead.Core.Persistence;

public sealed record CanonicalCatalogImportItem(
    GameId GameId,
    string ExternalId,
    string Name,
    string? Developer,
    string? Publisher,
    DateTimeOffset ObservedAtUtc);

public interface ICanonicalCatalogWriter
{
    Task ImportSteamAsync(
        IReadOnlyCollection<CanonicalCatalogImportItem> items,
        CancellationToken cancellationToken);
}
