using PlayStead.Core.Media;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Steam.Media;

public static class SteamMediaUriFactory
{
    public static IReadOnlyList<Uri> CreateCandidates(
        string appId,
        GameMediaAssetType assetType)
        => CreateCandidates(appId, mediaAssets: null, assetType);

    public static IReadOnlyList<Uri> CreateCandidates(
        string appId,
        SteamMediaAssetMetadata? mediaAssets,
        GameMediaAssetType assetType)
    {
        if (!long.TryParse(appId, out var parsedAppId) ||
            parsedAppId <= 0)
        {
            throw new ArgumentException(
                "Steam AppID must be a positive numeric value.",
                nameof(appId));
        }

        if (assetType == GameMediaAssetType.Cover)
        {
            var candidates = new List<Uri>();
            if (mediaAssets?.CoverAssets is { Count: > 0 } coverAssets)
            {
                foreach (var asset in coverAssets)
                {
                    if (IsSafeHash(asset.Hash) &&
                        IsSafeFileName(asset.FileName))
                    {
                        AddModernCandidates(candidates, appId, asset.Hash, asset.FileName);
                    }
                }
            }

            var libraryHash = mediaAssets?.Library600x900Hash;
            if (candidates.Count == 0 && IsSafeHash(libraryHash))
            {
                AddModernCandidates(candidates, appId, libraryHash!,
                    "library_600x900_2x.jpg", "library_600x900.jpg");
            }

            var capsuleHash = mediaAssets?.LibraryCapsuleHash;
            if (candidates.Count == 0 && IsSafeHash(capsuleHash))
            {
                AddModernCandidates(candidates, appId, capsuleHash!,
                    "library_capsule_2x.jpg", "library_capsule.jpg");
            }

            candidates.AddRange(CreateLegacyCandidates(appId));
            return candidates;
        }

        return assetType switch
        {
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

    private static IReadOnlyList<Uri> CreateLegacyCandidates(string appId) =>
    [
        new Uri(
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_capsule_2x.jpg",
            UriKind.Absolute),
        new Uri(
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_capsule.jpg",
            UriKind.Absolute),
        new Uri(
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900_2x.jpg",
            UriKind.Absolute),
        new Uri(
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
            UriKind.Absolute)
    ];

    private static void AddModernCandidates(
        ICollection<Uri> candidates,
        string appId,
        string hash,
        params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            candidates.Add(new Uri(
                $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appId}/{hash}/{fileName}",
                UriKind.Absolute));
        }
    }

    private static bool IsSafeHash(string? value) =>
        value is { Length: 40 } && value.All(Uri.IsHexDigit);

    private static bool IsSafeFileName(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.IndexOfAny(['/', '\\']) < 0 &&
        value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase);
}
