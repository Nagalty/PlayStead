namespace PlayStead.Core.Catalog;

public sealed record CatalogProviderRef(
    CatalogContentId ContentId,
    CatalogProviderKind Provider,
    string ExternalId,
    string? ExternalType,
    CatalogProvenance Provenance,
    CatalogConfidence Confidence,
    DateTimeOffset ObservedAtUtc);
