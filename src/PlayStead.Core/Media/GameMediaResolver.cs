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

        var cachedPath = _cache.TryGetPath(
            identity,
            assetType);

        if (cachedPath is not null)
        {
            Report(MediaResolutionEventKind.CacheHit, identity, assetType);
            return cachedPath;
        }

        Report(MediaResolutionEventKind.CacheMiss, identity, assetType);

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
                    return await _cache.StoreAsync(
                        identity,
                        payload,
                        cancellationToken)
                    .ConfigureAwait(false);
                }
                catch (InvalidDataException)
                {
                    Report(MediaResolutionEventKind.InvalidImage, identity, assetType);
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
                }

                continue;
            }
        }

        Report(MediaResolutionEventKind.FallbackUsed, identity, assetType);
        return null;
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
