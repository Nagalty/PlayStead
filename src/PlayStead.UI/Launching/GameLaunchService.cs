using PlayStead.Core.Library;

namespace PlayStead.UI.Launching;

public sealed class GameLaunchService
{
    private readonly IExternalUriLauncher
        _externalUriLauncher;

    public GameLaunchService(
        IExternalUriLauncher externalUriLauncher)
    {
        ArgumentNullException.ThrowIfNull(
            externalUriLauncher);

        _externalUriLauncher =
            externalUriLauncher;
    }

    public bool CanLaunch(
        GameInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(
            installation);

        return SteamLaunchUriFactory.CreateOrNull(
                   installation) is not null;
    }

    public bool TryLaunch(
        GameInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(
            installation);

        var uri =
            SteamLaunchUriFactory.CreateOrNull(
                installation);

        if (uri is null)
        {
            return false;
        }

        _externalUriLauncher.Open(
            uri);

        return true;
    }

    public bool CanOpenSteam(GameInstallation installation) =>
        SteamLaunchUriFactory.CreateStoreOrNull(installation) is not null;

    public bool TryOpenSteam(GameInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        var uri = SteamLaunchUriFactory.CreateStoreOrNull(installation);
        if (uri is null) return false;
        _externalUriLauncher.Open(uri);
        return true;
    }
}
