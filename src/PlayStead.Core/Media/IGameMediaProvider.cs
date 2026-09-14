namespace PlayStead.Core.Media;

public interface IGameMediaProvider
{
    bool CanResolve(GameMediaIdentity identity);

    Task<GameMediaPayload?> ResolveAsync(
        GameMediaIdentity identity,
        GameMediaAssetType assetType,
        CancellationToken cancellationToken);
}
