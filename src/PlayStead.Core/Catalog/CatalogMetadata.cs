namespace PlayStead.Core.Catalog;

public sealed record CatalogMetadata(
    int SchemaVersion,
    long CatalogVersion,
    DateTimeOffset GeneratedAtUtc);
