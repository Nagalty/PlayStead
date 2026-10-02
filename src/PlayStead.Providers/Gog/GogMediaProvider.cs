using PlayStead.Core.Library;
using PlayStead.Core.Media;

namespace PlayStead.Providers.Gog;

public sealed class GogMediaProvider : IGameMediaProvider
{
    private readonly GogLocalMediaLocator _locator;

    public GogMediaProvider(GogLocalMediaLocator locator)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    public bool CanResolve(GameMediaIdentity identity) =>
        identity.Provider == ProviderKind.Gog &&
        ulong.TryParse(identity.ProviderGameId, out _);

    public async Task<GameMediaPayload?> ResolveAsync(
        GameMediaIdentity identity,
        GameMediaAssetType assetType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();

        if (!CanResolve(identity) ||
            assetType is not (GameMediaAssetType.Cover or GameMediaAssetType.Hero))
        {
            return null;
        }

        var path = _locator.TryLocate(identity.ProviderGameId, assetType);
        if (path is null)
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllBytesAsync(path, cancellationToken)
                .ConfigureAwait(false);
            return new GameMediaPayload(
                assetType,
                "gog-local",
                identity.ProviderGameId,
                content,
                "image/webp",
                new Uri(Path.GetFullPath(path), UriKind.Absolute));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
