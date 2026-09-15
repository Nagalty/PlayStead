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
            GameMediaAssetType.Cover => [$"{appId}_library_600x900.jpg"],
            GameMediaAssetType.Header =>
            [
                $"{appId}_library_header.jpg",
                $"{appId}_header.jpg"
            ],
            GameMediaAssetType.Hero => [$"{appId}_library_hero.jpg"],
            GameMediaAssetType.Logo => [$"{appId}_logo.png"],
            _ => []
        };

        foreach (var filename in filenames)
        {
            var candidate = Path.Combine(
                steamRoot,
                "appcache",
                "librarycache",
                filename);

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }
}
