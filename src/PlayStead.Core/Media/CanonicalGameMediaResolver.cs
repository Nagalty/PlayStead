using PlayStead.Core.Identity;
using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using System.Collections.Concurrent;

namespace PlayStead.Core.Media;

public sealed class CanonicalGameMediaResolver : ICanonicalGameMediaResolver
{
    private readonly IProviderIdentityStore _identityStore;
    private readonly IGameMediaCache _cache;
    private readonly IReadOnlyList<IGameMediaProvider> _providers;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _singleFlight = new(StringComparer.Ordinal);

    public CanonicalGameMediaResolver(
        IProviderIdentityStore identityStore,
        IGameMediaCache cache,
        IEnumerable<IGameMediaProvider> providers)
    {
        _identityStore = identityStore ?? throw new ArgumentNullException(nameof(identityStore));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _providers = providers?.ToArray() ?? throw new ArgumentNullException(nameof(providers));
    }

    public string? TryGetCachedPath(GameId gameId, GameMediaAssetType assetType)
    {
        // Identity enumeration is asynchronous by design. The library keeps the
        // existing installation cache path until ResolveAndCacheAsync completes.
        // This method is intentionally conservative and never blocks the caller.
        return null;
    }

    public async Task<string?> ResolveAndCacheAsync(
        GameId gameId,
        string canonicalTitle,
        GameMediaAssetType assetType,
        ProviderKind? activeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalTitle);
        if (assetType != GameMediaAssetType.Logo)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var key = $"{gameId.Value:D}|{assetType}|{activeProvider?.ToString() ?? "none"}";
        var gate = _singleFlight.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ResolveCoreAsync(
                gameId,
                canonicalTitle,
                assetType,
                activeProvider,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string?> ResolveCoreAsync(
        GameId gameId,
        string canonicalTitle,
        GameMediaAssetType assetType,
        ProviderKind? activeProvider,
        CancellationToken cancellationToken)
    {
        var identities = await _identityStore.GetByGameIdAsync(gameId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var identity in OrderIdentities(identities, activeProvider))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mediaIdentity = new GameMediaIdentity(
                identity.Provider,
                identity.ExternalId,
                canonicalTitle);

            var cached = _cache.TryGetPath(mediaIdentity, assetType);
            if (cached is not null)
            {
                var cachedSource = _cache.TryGetSourceUri(mediaIdentity, assetType);
                if (assetType != GameMediaAssetType.Logo || IsValidLogoSource(cachedSource))
                {
                    return cached;
                }
            }

            foreach (var provider in _providers)
            {
                if (!provider.CanResolve(mediaIdentity))
                {
                    continue;
                }

                GameMediaPayload? payload;
                try
                {
                    payload = await provider.ResolveAsync(
                        mediaIdentity,
                        assetType,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (
                    ex is HttpRequestException
                    or IOException
                    or InvalidDataException
                    or UnauthorizedAccessException)
                {
                    continue;
                }

                if (payload is null || !IsValidLogo(payload))
                {
                    continue;
                }

                try
                {
                    var path = await _cache.StoreAsync(
                        mediaIdentity,
                        payload,
                        cancellationToken).ConfigureAwait(false);
                    return path;
                }
                catch (InvalidDataException)
                {
                    // Invalid media from one identity must not prevent another
                    // reliable identity from supplying the canonical logo.
                }
            }
        }

        return null;
    }

    private static IEnumerable<GameProviderIdentity> OrderIdentities(
        IReadOnlyList<GameProviderIdentity> identities,
        ProviderKind? activeProvider) =>
        identities
            .Where(identity => identity.Confidence == CatalogConfidence.Deterministic)
            .OrderByDescending(identity => identity.Provider == activeProvider)
            .ThenByDescending(identity => identity.Source == ProviderIdentitySource.UserConfirmed)
            .ThenByDescending(identity => identity.Source == ProviderIdentitySource.CanonicalCatalog)
            .ThenByDescending(identity => identity.UpdatedAtUtc)
            .ThenBy(identity => identity.Provider)
            .ThenBy(identity => identity.ExternalId, StringComparer.Ordinal);

    private static bool IsValidLogo(GameMediaPayload payload)
    {
        if (payload.AssetType != GameMediaAssetType.Logo || payload.Content.Length == 0)
        {
            return false;
        }

        var source = string.Join(
            '/',
            payload.Source,
            payload.SourceUri?.AbsolutePath ?? string.Empty,
            payload.ExternalId).ToLowerInvariant();

        return IsValidLogoSource(source);
    }

    private static bool IsValidLogoSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return true;
        }

        source = source.ToLowerInvariant();
        return !source.Contains("square_icon", StringComparison.Ordinal)
            && !source.Contains("appicon", StringComparison.Ordinal)
            && !source.Contains("app_icon", StringComparison.Ordinal)
            && !source.Contains("portrait", StringComparison.Ordinal)
            && !source.Contains("cover", StringComparison.Ordinal)
            && !source.Contains("hero", StringComparison.Ordinal)
            && !source.Contains("background", StringComparison.Ordinal);
    }
}
