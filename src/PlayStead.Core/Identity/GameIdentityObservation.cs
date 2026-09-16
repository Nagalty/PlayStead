using PlayStead.Core.Catalog;

namespace PlayStead.Core.Identity;

public sealed record GameIdentityObservation(
    CatalogProviderKind Provider,
    string ExternalId,
    string Title,
    DateTimeOffset ObservedAtUtc);
