namespace PlayStead.Core.Media;

public sealed class GameMediaResolver : IGameMediaResolver
{
    private readonly IGameMediaCache _cache;
    private readonly IReadOnlyList<IGameMediaProvider> _providers;
    private readonly IMediaDiagnostics _diagnostics;

    public GameMediaResolver(
        IGameMediaCache cache,
        IEnumerable<IGameMediaProvider> providers,
        IMediaDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(providers);

        _cache = cache;
        _providers = providers.ToArray();
        _diagnostics = diagnostics ?? NoOpMediaDiagnostics.Instance;
    }

    public string? TryGetCachedPath(
        GameMediaIdentity identity,
        GameMediaAssetType assetType)
    {
        return _cache.TryGetPath(identity, assetType);
    }

    public async Task<string?> ResolveAndCacheAsync(
        GameMediaIdentity identity,
        GameMediaAssetType assetType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TraceDetail(identity, assetType, $"Request=YES");

        var cachedPath = _cache.TryGetPath(
            identity,
            assetType);

        if (cachedPath is not null)
        {
            Report(MediaResolutionEventKind.CacheHit, identity, assetType);
            TraceDetail(identity, assetType, "Uri=CachePath Resolved=YES Source=LocalCache");
            return cachedPath;
        }

        Report(MediaResolutionEventKind.CacheMiss, identity, assetType);
        var failureReason = "NoProviderPayload";

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!provider.CanResolve(identity))
            {
                continue;
            }

            try
            {
                var payload = await provider.ResolveAsync(
                        identity,
                        assetType,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (payload is null)
                {
                    continue;
                }

                try
                {
                    var cachedResult = await _cache.StoreAsync(
                        identity,
                        payload,
                        cancellationToken)
                        .ConfigureAwait(false);
                    TraceDetail(identity, assetType,
                        $"Uri={payload.SourceUri?.AbsoluteUri ?? "Payload"} Resolved=YES Source={payload.Source}");
                    return cachedResult;
                }
                catch (InvalidDataException)
                {
                    Report(MediaResolutionEventKind.InvalidImage, identity, assetType);
                    failureReason = "InvalidImage";
                }
            }
            catch (Exception ex) when (
                ex is HttpRequestException
                or IOException
                or InvalidDataException
                or UnauthorizedAccessException)
            {
                if (ex is InvalidDataException)
                {
                    Report(MediaResolutionEventKind.InvalidImage, identity, assetType);
                    failureReason = "InvalidImage";
                }

                failureReason = ex.GetType().Name;

                continue;
            }
        }

        Report(MediaResolutionEventKind.FallbackUsed, identity, assetType);
        TraceDetail(identity, assetType, $"Uri=None Resolved=NO Source=None Reason={failureReason}");
        return null;
    }

    private static void TraceDetail(
        GameMediaIdentity identity,
        GameMediaAssetType assetType,
        string details)
    {
        if (assetType is not (GameMediaAssetType.Cover or GameMediaAssetType.Hero))
        {
            return;
        }

        var label = assetType == GameMediaAssetType.Cover ? "Cover" : "Hero";
        var normalizedDetails = details
            .Replace("Uri=", $"{label}Uri=", StringComparison.Ordinal)
            .Replace("Resolved=", $"{label}Resolved=", StringComparison.Ordinal)
            .Replace("Source=", $"{label}Source=", StringComparison.Ordinal);
        System.Diagnostics.Trace.WriteLine(
            $"[MEDIA-DETAIL] Game=\"{identity.CanonicalTitle}\" Identity={identity.Provider}:{identity.ProviderGameId} Asset={assetType} {normalizedDetails}");
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
