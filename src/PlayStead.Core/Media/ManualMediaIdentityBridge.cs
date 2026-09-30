using PlayStead.Core.Library;
using PlayStead.Core.Persistence;

namespace PlayStead.Core.Media;

public sealed class ManualMediaIdentityBridge : IGameMediaResolver
{
    private readonly IGameMediaResolver _inner;
    private readonly IManualMetadataLinkStore _links;

    public ManualMediaIdentityBridge(IGameMediaResolver inner, IManualMetadataLinkStore links)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _links = links ?? throw new ArgumentNullException(nameof(links));
    }

    public string? TryGetCachedPath(GameMediaIdentity identity, GameMediaAssetType assetType)
    {
        var translated = Translate(identity);
        return translated is null ? null : _inner.TryGetCachedPath(translated, assetType);
    }

    public async Task<string?> ResolveAndCacheAsync(GameMediaIdentity identity, GameMediaAssetType assetType, CancellationToken cancellationToken)
    {
        var translated = identity.Provider == ProviderKind.Manual
            ? await TranslateAsync(identity, cancellationToken).ConfigureAwait(false)
            : identity;
        return translated is null
            ? null
            : await _inner.ResolveAndCacheAsync(translated, assetType, cancellationToken).ConfigureAwait(false);
    }

    private GameMediaIdentity? Translate(GameMediaIdentity identity)
    {
        if (identity.Provider != ProviderKind.Manual)
            return identity;
        var link = _links.TryGetCached(ParseGameId(identity.ProviderGameId));
        return link?.MediaSource is null
            ? null
            : new GameMediaIdentity(link.MediaSource.Provider, link.MediaSource.ExternalId, identity.CanonicalTitle);
    }

    private async Task<GameMediaIdentity?> TranslateAsync(GameMediaIdentity identity, CancellationToken cancellationToken)
    {
        if (identity.Provider != ProviderKind.Manual)
            return identity;
        var link = await _links.GetAsync(ParseGameId(identity.ProviderGameId), cancellationToken).ConfigureAwait(false);
        return link?.MediaSource is null
            ? null
            : new GameMediaIdentity(link.MediaSource.Provider, link.MediaSource.ExternalId, identity.CanonicalTitle);
    }

    private static GameId ParseGameId(string value) =>
        value.StartsWith("manual:", StringComparison.OrdinalIgnoreCase) &&
        Guid.TryParse(value[7..], out var id)
            ? new GameId(id)
            : throw new ArgumentException("Manual media identity must contain manual:<guid>.", nameof(value));
}
