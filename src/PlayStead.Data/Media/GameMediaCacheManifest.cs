namespace PlayStead.Data.Media;

internal sealed record GameMediaCacheManifest(
    Dictionary<string, GameMediaCacheEntry> Assets);

internal sealed record GameMediaCacheEntry(
    string Source,
    string ExternalId,
    string AssetType,
    string FileName,
    DateTimeOffset RetrievedAtUtc,
    string? SourceUri,
    string? ContentType);
