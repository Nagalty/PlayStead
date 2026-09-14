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

        if (assetType != GameMediaAssetType.Cover)
        {
            throw new NotSupportedException(
                $"Steam media asset type '{assetType}' is not supported in this task.");
        }

        return
        [
            new Uri(
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
                UriKind.Absolute)
        ];
    }
}
