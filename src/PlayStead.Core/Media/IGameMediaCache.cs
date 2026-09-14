namespace PlayStead.Core.Media;

public interface IGameMediaCache
{
    string? TryGetPath(
        GameMediaIdentity identity,
        GameMediaAssetType assetType);

    Task<string> StoreAsync(
        GameMediaIdentity identity,
        GameMediaPayload payload,
        CancellationToken cancellationToken);
}
