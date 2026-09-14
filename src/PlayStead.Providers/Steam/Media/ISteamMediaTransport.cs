using PlayStead.Core.Media;

namespace PlayStead.Providers.Steam.Media;

public interface ISteamMediaTransport
{
    Task<GameMediaPayload?> TryDownloadAsync(
        string appId,
        GameMediaAssetType assetType,
        IReadOnlyList<Uri> candidates,
        CancellationToken cancellationToken);
}
