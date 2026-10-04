using PlayStead.Core.Media;

namespace PlayStead.Providers.Epic;

public sealed class EpicLocalMediaLocator
{
    private static readonly string[] CoverTokens = ["dieselgameboxtall", "offerimagetall", "tall", "portrait", "cover"];
    private static readonly string[] HeroTokens = ["dieselgamebox", "offerimagewide", "wide", "background", "backdrop", "hero"];
    private readonly IReadOnlyList<string> _roots;

    public EpicLocalMediaLocator(IEnumerable<string> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        _roots = roots.Where(Directory.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public string? TryLocate(string catalogItemId, GameMediaAssetType assetType)
    {
        if (string.IsNullOrWhiteSpace(catalogItemId) || assetType is not (GameMediaAssetType.Cover or GameMediaAssetType.Hero))
            return null;

        var tokens = assetType == GameMediaAssetType.Cover ? CoverTokens : HeroTokens;
        try
        {
            return _roots.SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                .Where(IsImage)
                .Where(path => Path.GetFileName(path).Contains(catalogItemId, StringComparison.OrdinalIgnoreCase))
                .Where(path => tokens.Any(token => Path.GetFileNameWithoutExtension(path).Contains(token, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsImage(string path) =>
        Path.GetExtension(path) is ".jpg" or ".jpeg" or ".png" or ".webp";
}
