namespace PlayStead.Data.Catalog;

public sealed record CanonicalCatalogSyncOptions(
    Uri ManifestUri,
    string CacheDirectory,
    long MaximumPayloadBytes = 128L * 1024 * 1024,
    TimeSpan? Timeout = null);
