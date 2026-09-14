using PlayStead.Core.Library;

namespace PlayStead.UI.Launching;

public static class SteamLaunchUriFactory
{
    public static Uri? CreateOrNull(
        GameInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(
            installation);

        if (installation.Provider !=
                ProviderKind.Steam ||
            !installation.IsPresent ||
            string.IsNullOrWhiteSpace(
                installation.ExternalId))
        {
            return null;
        }

        return new Uri(
            $"steam://rungameid/{installation.ExternalId.Trim()}",
            UriKind.Absolute);
    }
}
