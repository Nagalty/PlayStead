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

        string[] filenames = assetType switch
        {
            GameMediaAssetType.Cover => ["library_600x900.jpg"],
            GameMediaAssetType.Header =>
            [
                "library_header.jpg",
                "header.jpg"
            ],
            GameMediaAssetType.Hero => ["library_hero.jpg"],
            GameMediaAssetType.Logo => ["logo.png"],
            _ => []
        };

        foreach (var filename in filenames)
        {
            var candidate = Path.Combine(
                steamRoot,
                "appcache",
                "librarycache",
                appId,
                filename);

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }
}
