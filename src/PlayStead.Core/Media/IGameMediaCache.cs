namespace PlayStead.Core.Media;

public interface IGameMediaCache
{
    string? TryGetPath(
        GameMediaIdentity identity,
        GameMediaAssetType assetType);

    string? TryGetSourceUri(
        GameMediaIdentity identity,
        GameMediaAssetType assetType) => null;

    Task<string> StoreAsync(
        GameMediaIdentity identity,
        GameMediaPayload payload,
        CancellationToken cancellationToken);
}
