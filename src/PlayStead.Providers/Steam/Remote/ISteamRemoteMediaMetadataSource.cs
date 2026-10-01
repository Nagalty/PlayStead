using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Steam.Remote;

public interface ISteamRemoteMediaMetadataSource
{
    Task<SteamMediaAssetMetadata?> GetAsync(
        string appId,
        CancellationToken cancellationToken);
}
