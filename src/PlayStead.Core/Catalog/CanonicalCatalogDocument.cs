namespace PlayStead.Core.Catalog;

public sealed record CanonicalCatalogDocument(
    int SchemaVersion,
    long CatalogVersion,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<CanonicalCatalogEntry> Entries);

public sealed record CanonicalCatalogEntry(
    CatalogContentId Id,
    PlaySteadPublicId PublicId,
    string CanonicalTitle,
    string NormalizedTitle,
    DateOnly? ReleaseDate,
    string? Developer,
    string? Publisher,
    IReadOnlyList<string> Genres,
    IReadOnlyList<CanonicalCatalogProviderReference> ProviderRefs,
    CatalogProvenance Provenance,
    DateTimeOffset ObservedAtUtc);

public sealed record CanonicalCatalogProviderReference(
    CatalogProviderKind Provider,
    string ExternalId,
    string? ExternalType,
    CatalogProvenance Provenance,
    CatalogConfidence Confidence,
    DateTimeOffset ObservedAtUtc);

public sealed record CanonicalCatalogManifest(
    int SchemaVersion,
    long CatalogVersion,
    DateTimeOffset GeneratedAtUtc,
    string PayloadUrl,
    string PayloadSha256,
    int EntryCount,
    long PayloadSizeBytes = 0,
    string? PayloadEncoding = null,
    long PayloadUncompressedSizeBytes = 0);
