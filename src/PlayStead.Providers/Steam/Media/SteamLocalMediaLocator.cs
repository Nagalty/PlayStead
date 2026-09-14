using PlayStead.Core.Media;

namespace PlayStead.Providers.Steam.Media;

public sealed class SteamLocalMediaLocator
{
    public string? TryLocate(
        string steamRoot,
        string appId,
        GameMediaAssetType assetType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(steamRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        if (assetType != GameMediaAssetType.Cover)
        {
            return null;
        }

        var candidate = Path.Combine(
            steamRoot,
            "appcache",
            "librarycache",
            $"{appId}_library_600x900.jpg");

        return File.Exists(candidate)
            ? Path.GetFullPath(candidate)
            : null;
    }
}
