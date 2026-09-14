namespace PlayStead.Core.Media;

public sealed record GameMediaPayload(
    GameMediaAssetType AssetType,
    string Source,
    string ExternalId,
    byte[] Content,
    string? ContentType,
    Uri? SourceUri);
