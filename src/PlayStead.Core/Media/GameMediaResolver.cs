namespace PlayStead.Core.Media;

public sealed class GameMediaResolver : IGameMediaResolver
{
    private readonly IGameMediaCache _cache;
    private readonly IReadOnlyList<IGameMediaProvider> _providers;

    public GameMediaResolver(
        IGameMediaCache cache,
        IEnumerable<IGameMediaProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(providers);

        _cache = cache;
        _providers = providers.ToArray();
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
            return cachedPath;
        }

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

                return await _cache.StoreAsync(
                        identity,
                        payload,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is HttpRequestException
                or IOException
                or InvalidDataException
                or UnauthorizedAccessException)
            {
                continue;
            }
        }

        return null;
    }
}
