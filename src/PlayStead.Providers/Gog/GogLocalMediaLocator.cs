using PlayStead.Core.Media;

namespace PlayStead.Providers.Gog;

public sealed class GogLocalMediaLocator
{
    private readonly string _webCacheRoot;

    public GogLocalMediaLocator(string? webCacheRoot = null)
    {
        _webCacheRoot = Path.GetFullPath(
            string.IsNullOrWhiteSpace(webCacheRoot)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "GOG.com",
                    "Galaxy",
                    "webcache")
                : webCacheRoot);
    }

    public string? TryLocate(string productId, GameMediaAssetType assetType)
    {
        if (!ulong.TryParse(productId, out _) ||
            assetType is not (GameMediaAssetType.Cover or GameMediaAssetType.Hero))
        {
            return null;
        }

        try
        {
            if (!Directory.Exists(_webCacheRoot))
            {
                return null;
            }

            foreach (var cacheInstance in Directory.EnumerateDirectories(_webCacheRoot)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var productDirectory = Path.Combine(cacheInstance, "gog", productId);
                if (!Directory.Exists(productDirectory))
                {
                    continue;
                }

                var suffix = assetType == GameMediaAssetType.Cover
                    ? "_glx_vertical_cover.webp"
                    : "_glx_bg_top_padding_7.webp";

                var candidate = Directory.EnumerateFiles(
                        productDirectory,
                        "*" + suffix,
                        SearchOption.TopDirectoryOnly)
                    .Where(IsUsableWebp)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (candidate is not null)
                {
                    return candidate;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    private static bool IsUsableWebp(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 16)
            {
                return false;
            }

            Span<byte> header = stackalloc byte[12];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return stream.Read(header) == header.Length &&
                header[..4].SequenceEqual("RIFF"u8) &&
                header[8..].SequenceEqual("WEBP"u8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
