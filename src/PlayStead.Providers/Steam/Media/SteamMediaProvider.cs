using PlayStead.Core.Library;
using PlayStead.Core.Media;

namespace PlayStead.Providers.Steam.Media;

public sealed class SteamMediaProvider : IGameMediaProvider
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLocalMediaLocator _localLocator;
    private readonly ISteamMediaTransport _transport;
    private readonly ISteamStoreAppDetailsClient? _storeClient;
    private readonly SteamAppInfoReader _appInfoReader;
    private readonly IMediaDiagnostics _diagnostics;

    public SteamMediaProvider(
        WindowsSteamRootLocator rootLocator,
        SteamLocalMediaLocator localLocator,
        ISteamMediaTransport transport,
        IMediaDiagnostics? diagnostics = null,
        ISteamStoreAppDetailsClient? storeClient = null,
        SteamAppInfoReader? appInfoReader = null)
    {
        ArgumentNullException.ThrowIfNull(rootLocator);
        ArgumentNullException.ThrowIfNull(localLocator);
        ArgumentNullException.ThrowIfNull(transport);

        _rootLocator = rootLocator;
        _localLocator = localLocator;
        _transport = transport;
        _storeClient = storeClient;
        _appInfoReader = appInfoReader ?? new SteamAppInfoReader();
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
        var mediaAssets = steamRoot is null
            ? null
            : ReadMediaAssets(steamRoot, identity.ProviderGameId);

        if (steamRoot is not null)
        {
            var localCoverPath = _localLocator.TryLocate(
                steamRoot,
                identity.ProviderGameId,
                assetType,
                mediaAssets?.LibraryAssetHash);

            if (localCoverPath is not null)
            {
                var localBytes = await File.ReadAllBytesAsync(
                        localCoverPath,
                        cancellationToken)
                    .ConfigureAwait(false);

                Report(MediaResolutionEventKind.LocalProviderHit, identity, assetType);
                System.Diagnostics.Trace.WriteLine(
                    $"[STEAM-MEDIA] AppId={identity.ProviderGameId} Asset={assetType} Source=Local Candidate={Path.GetFileName(localCoverPath)} Result=Success");

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
            mediaAssets,
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

    private SteamMediaAssetMetadata? ReadMediaAssets(
        string steamRoot,
        string appId)
    {
        if (!uint.TryParse(appId, out var parsedAppId))
        {
            return null;
        }

        try
        {
            return _appInfoReader.Find(
                Path.Combine(steamRoot, "appcache", "appinfo.vdf"),
                parsedAppId)?.MediaAssets;
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or InvalidDataException)
        {
            return null;
        }
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
