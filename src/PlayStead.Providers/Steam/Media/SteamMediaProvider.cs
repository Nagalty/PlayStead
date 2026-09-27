using PlayStead.Core.Library;
using PlayStead.Core.Media;

namespace PlayStead.Providers.Steam.Media;

public sealed class SteamMediaProvider : IGameMediaProvider
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLocalMediaLocator _localLocator;
    private readonly ISteamMediaTransport _transport;
    private readonly ISteamStoreAppDetailsClient? _storeClient;
    private readonly IMediaDiagnostics _diagnostics;

    public SteamMediaProvider(
        WindowsSteamRootLocator rootLocator,
        SteamLocalMediaLocator localLocator,
        ISteamMediaTransport transport,
        IMediaDiagnostics? diagnostics = null,
        ISteamStoreAppDetailsClient? storeClient = null)
    {
        ArgumentNullException.ThrowIfNull(rootLocator);
        ArgumentNullException.ThrowIfNull(localLocator);
        ArgumentNullException.ThrowIfNull(transport);

        _rootLocator = rootLocator;
        _localLocator = localLocator;
        _transport = transport;
        _storeClient = storeClient;
        _diagnostics = diagnostics ?? NoOpMediaDiagnostics.Instance;
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

                Report(MediaResolutionEventKind.LocalProviderHit, identity, assetType);

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
        if (assetType == GameMediaAssetType.Header && _storeClient is not null)
        {
            var details = await _storeClient.GetAsync(identity.ProviderGameId, cancellationToken).ConfigureAwait(false);
            if (details?.HeaderImageUri is { } header)
                remoteCandidates = new[] { header }.Concat(remoteCandidates).ToArray();
        }

        GameMediaPayload? payload;
        try
        {
            payload = await _transport.TryDownloadAsync(
                    identity.ProviderGameId,
                    assetType,
                    remoteCandidates,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            Report(MediaResolutionEventKind.RemoteProviderFailure, identity, assetType);
            throw;
        }

        Report(
            payload is null
                ? MediaResolutionEventKind.RemoteProviderFailure
                : MediaResolutionEventKind.RemoteProviderSuccess,
            identity,
            assetType);

        return payload;
    }

    private void Report(
        MediaResolutionEventKind kind,
        GameMediaIdentity identity,
        GameMediaAssetType assetType)
    {
        try
        {
            _diagnostics.Report(new MediaResolutionEvent(
                kind,
                identity.Provider,
                identity.ProviderGameId,
                assetType));
        }
        catch
        {
            // Diagnostics must never change media resolution behavior.
        }
    }

    private sealed class NoOpMediaDiagnostics : IMediaDiagnostics
    {
        public static NoOpMediaDiagnostics Instance { get; } = new();

        public void Report(MediaResolutionEvent mediaEvent)
        {
        }
    }
}
