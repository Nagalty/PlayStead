using PlayStead.Core.Library;
using System.Globalization;

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

    public static Uri? CreateStoreOrNull(GameInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        if (installation.Provider != ProviderKind.Steam ||
            !uint.TryParse(installation.ExternalId?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var appId) ||
            appId == 0)
        {
            return null;
        }

        return new Uri($"steam://openurl/https://store.steampowered.com/app/{appId}", UriKind.Absolute);
    }
}
