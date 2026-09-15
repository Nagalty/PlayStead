using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamLocalMediaLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryLocate_Header_prefers_library_header_then_store_header_then_returns_null()
    {
        var fallback = CreateLocalAsset("1874880_header.jpg");
        var preferred = CreateLocalAsset("1874880_library_header.jpg");
        var locator = new SteamLocalMediaLocator();

        Assert.Equal(preferred,
            locator.TryLocate(_root, "1874880", GameMediaAssetType.Header));

        File.Delete(preferred);

        Assert.Equal(fallback,
            locator.TryLocate(_root, "1874880", GameMediaAssetType.Header));

        File.Delete(fallback);

        Assert.Null(locator.TryLocate(_root, "1874880", GameMediaAssetType.Header));
    }

    [Theory]
    [InlineData(GameMediaAssetType.Hero, "1874880_library_hero.jpg")]
    [InlineData(GameMediaAssetType.Logo, "1874880_logo.png")]
    public void TryLocate_returns_exact_local_asset_then_null_when_missing(
        GameMediaAssetType assetType,
        string filename)
    {
        var expected = CreateLocalAsset(filename);
        var locator = new SteamLocalMediaLocator();

        Assert.Equal(expected, locator.TryLocate(_root, "1874880", assetType));
        Assert.Null(locator.TryLocate(_root, "18748", assetType));

        File.Delete(expected);

        Assert.Null(locator.TryLocate(_root, "1874880", assetType));
    }

    [Fact]
    public void Locator_exposes_Task3_cover_lookup_contract()
    {
        var method = typeof(SteamLocalMediaLocator).GetMethod(
            "TryLocate",
            [
                typeof(string),
                typeof(string),
                typeof(GameMediaAssetType)
            ]);

        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
    }

    private string CreateLocalAsset(string filename)
    {
        var directory = Path.Combine(_root, "appcache", "librarycache");
        Directory.CreateDirectory(directory);
        var path = Path.GetFullPath(Path.Combine(directory, filename));
        // The locator checks file existence, not image decoding.
        File.WriteAllBytes(path, [1]);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
