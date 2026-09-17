namespace PlayStead.Core.Media;

public interface ILocalGameMediaResolver
{
    string? TryGetPath(
        GameMediaIdentity identity,
        GameMediaAssetType assetType);
}
