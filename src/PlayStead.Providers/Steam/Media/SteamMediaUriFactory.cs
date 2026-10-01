using PlayStead.Core.Media;

namespace PlayStead.Providers.Steam.Media;

public static class SteamMediaUriFactory
{
    public static IReadOnlyList<Uri> CreateCandidates(
        string appId,
        GameMediaAssetType assetType)
    {
        if (!long.TryParse(appId, out var parsedAppId) ||
            parsedAppId <= 0)
        {
            throw new ArgumentException(
                "Steam AppID must be a positive numeric value.",
                nameof(appId));
        }

        return assetType switch
        {
            GameMediaAssetType.Cover =>
            [
                new Uri(
                    $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900_2x.jpg",
                    UriKind.Absolute),
                new Uri(
                    $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
                    UriKind.Absolute)
            ],
            GameMediaAssetType.Header =>
            [
                new Uri(
                    $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_header.jpg",
                    UriKind.Absolute),
                new Uri(
                    $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
                    UriKind.Absolute)
            ],
            GameMediaAssetType.Hero =>
            [
                new Uri(
                    $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_hero.jpg",
                    UriKind.Absolute)
            ],
            GameMediaAssetType.Logo =>
            [
                new Uri(
                    $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/logo.png",
                    UriKind.Absolute)
            ],
            _ => throw new NotSupportedException(
                $"Steam media asset type '{assetType}' is not supported in this task.")
        };
    }
}
