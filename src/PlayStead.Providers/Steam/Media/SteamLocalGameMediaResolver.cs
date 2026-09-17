using PlayStead.Core.Library;
using PlayStead.Core.Media;

namespace PlayStead.Providers.Steam.Media;

public sealed class SteamLocalGameMediaResolver : ILocalGameMediaResolver
{
    private readonly WindowsSteamRootLocator _rootLocator;
    private readonly SteamLocalMediaLocator _localLocator;

    public SteamLocalGameMediaResolver(
        WindowsSteamRootLocator rootLocator,
        SteamLocalMediaLocator localLocator)
    {
        ArgumentNullException.ThrowIfNull(rootLocator);
        ArgumentNullException.ThrowIfNull(localLocator);
        _rootLocator = rootLocator;
        _localLocator = localLocator;
    }

    public string? TryGetPath(
        GameMediaIdentity identity,
        GameMediaAssetType assetType)
    {
        ArgumentNullException.ThrowIfNull(identity);

        if (identity.Provider != ProviderKind.Steam)
        {
            return null;
        }

        var steamRoot = _rootLocator.TryLocate();
        return steamRoot is null
            ? null
            : _localLocator.TryLocate(
                steamRoot,
                identity.ProviderGameId,
                assetType);
    }
}
