namespace PlayStead.Core.Media;

public interface IGameMediaResolver
{
    string? TryGetCachedPath(
        GameMediaIdentity identity,
        GameMediaAssetType assetType);

    Task<string?> ResolveAndCacheAsync(
        GameMediaIdentity identity,
        GameMediaAssetType assetType,
        CancellationToken cancellationToken);
}
