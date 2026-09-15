using PlayStead.Core.Library;
using PlayStead.Core.Media;

namespace PlayStead.Providers.Steam.Media;

public sealed class SteamMediaProvider : IGameMediaProvider
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLocalMediaLocator _localLocator;
    private readonly ISteamMediaTransport _transport;

    public SteamMediaProvider(
        WindowsSteamRootLocator rootLocator,
        SteamLocalMediaLocator localLocator,
        ISteamMediaTransport transport)
    {
        ArgumentNullException.ThrowIfNull(rootLocator);
        ArgumentNullException.ThrowIfNull(localLocator);
        ArgumentNullException.ThrowIfNull(transport);

        _rootLocator = rootLocator;
        _localLocator = localLocator;
        _transport = transport;
    }

    public bool CanResolve(GameMediaIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return identity.Provider == ProviderKind.Steam;
    }

    public async Task<GameMediaPayload?> ResolveAsync(
        GameMediaIdentity identity,
        GameMediaAssetType assetType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        if (assetType is not (GameMediaAssetType.Cover or
            GameMediaAssetType.Header or
            GameMediaAssetType.Hero or
            GameMediaAssetType.Logo) ||
            identity.Provider != ProviderKind.Steam)
        {
            return null;
        }

        var steamRoot = _rootLocator.TryLocate();

        if (steamRoot is not null)
        {
            var localCoverPath = _localLocator.TryLocate(
                steamRoot,
                identity.ProviderGameId,
                assetType);

            if (localCoverPath is not null)
            {
                var localBytes = await File.ReadAllBytesAsync(
                        localCoverPath,
                        cancellationToken)
                    .ConfigureAwait(false);

                return new GameMediaPayload(
                    assetType,
                    "steam-local",
                    identity.ProviderGameId,
                    localBytes,
                    assetType == GameMediaAssetType.Logo
                        ? "image/png"
                        : "image/jpeg",
                    new Uri(
                        Path.GetFullPath(localCoverPath),
                        UriKind.Absolute));
            }
        }

        var remoteCandidates = SteamMediaUriFactory.CreateCandidates(
            identity.ProviderGameId,
            assetType);

        return await _transport.TryDownloadAsync(
                identity.ProviderGameId,
                assetType,
                remoteCandidates,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
