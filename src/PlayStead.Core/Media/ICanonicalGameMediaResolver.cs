using PlayStead.Core.Library;

namespace PlayStead.Core.Media;

public interface ICanonicalGameMediaResolver
{
    string? TryGetCachedPath(GameId gameId, GameMediaAssetType assetType);

    Task<string?> ResolveAndCacheAsync(
        GameId gameId,
        string canonicalTitle,
        GameMediaAssetType assetType,
        ProviderKind? activeProvider,
        CancellationToken cancellationToken);
}
