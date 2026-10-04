using PlayStead.Core.Library;

namespace PlayStead.UI.Launching;

public static class EpicLaunchUriFactory
{
    public static Uri? CreateOrNull(GameInstallation? installation)
    {
        if (installation is null ||
            installation.Provider != ProviderKind.Epic ||
            !installation.IsPresent ||
            installation.LaunchMetadata is not { Provider: ProviderKind.Epic } metadata)
        {
            return null;
        }

        var catalogItemId = metadata["CatalogItemId"];
        var catalogNamespace = metadata["CatalogNamespace"];
        var appName = metadata["AppName"];
        if (string.IsNullOrWhiteSpace(catalogItemId) ||
            string.IsNullOrWhiteSpace(catalogNamespace) ||
            string.IsNullOrWhiteSpace(appName))
        {
            return null;
        }

        var tuple = string.Join(':', catalogNamespace.Trim(), catalogItemId.Trim(), appName.Trim());
        var encodedTuple = Uri.EscapeDataString(tuple);
        return new Uri($"com.epicgames.launcher://apps/{encodedTuple}?action=launch&silent=true", UriKind.Absolute);
    }
}
