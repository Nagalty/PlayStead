using PlayStead.Core.Library;

namespace PlayStead.Core.Media;

public enum MediaResolutionEventKind
{
    CacheHit,
    CacheMiss,
    LocalProviderHit,
    RemoteProviderSuccess,
    RemoteProviderFailure,
    InvalidImage,
    FallbackUsed
}

public sealed record MediaResolutionEvent(
    MediaResolutionEventKind Kind,
    ProviderKind Provider,
    string ProviderGameId,
    GameMediaAssetType AssetType);
