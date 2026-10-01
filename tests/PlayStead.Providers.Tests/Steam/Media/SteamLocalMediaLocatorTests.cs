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
        var fallback = CreateLocalAsset("1874880", "header.jpg");
        var preferred = CreateLocalAsset("1874880", "library_header.jpg");
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
    [InlineData(GameMediaAssetType.Hero, "library_hero.jpg")]
    [InlineData(GameMediaAssetType.Logo, "logo.png")]
    public void TryLocate_returns_exact_local_asset_then_null_when_missing(
        GameMediaAssetType assetType,
        string filename)
    {
        var expected = CreateLocalAsset("1874880", filename);
        var locator = new SteamLocalMediaLocator();

        Assert.Equal(expected, locator.TryLocate(_root, "1874880", assetType));
        Assert.Null(locator.TryLocate(_root, "18748", assetType));

        File.Delete(expected);

        Assert.Null(locator.TryLocate(_root, "1874880", assetType));
    }

    [Theory]
    [InlineData(GameMediaAssetType.Cover, "library_capsule_2x.jpg")]
    [InlineData(GameMediaAssetType.Cover, "library_capsule.jpg")]
    [InlineData(GameMediaAssetType.Cover, "library_600x900_2x.jpg")]
    [InlineData(GameMediaAssetType.Cover, "library_600x900.jpg")]
    [InlineData(GameMediaAssetType.Header, "library_header.jpg")]
    [InlineData(GameMediaAssetType.Hero, "library_hero.jpg")]
    [InlineData(GameMediaAssetType.Logo, "logo.png")]
    public void TryLocate_returns_asset_from_direct_hash_directory(
        GameMediaAssetType assetType,
        string filename)
    {
        var expected = CreateNestedAsset(
            "2116120",
            "5c3871f559599645d024432ff4e791a234c53976",
            filename);
        var locator = new SteamLocalMediaLocator();

        Assert.Equal(
            expected,
            locator.TryLocate(_root, "2116120", assetType));
    }

    [Fact]
    public void TryLocate_cover_prefers_modern_2x_then_modern_then_legacy_2x_then_legacy()
    {
        var modern2x = CreateNestedAsset("1874880", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "library_capsule_2x.jpg");
        var modern = CreateNestedAsset("1874880", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "library_capsule.jpg");
        var legacy2x = CreateNestedAsset("1874880", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "library_600x900_2x.jpg");
        var legacy = CreateNestedAsset("1874880", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "library_600x900.jpg");
        var locator = new SteamLocalMediaLocator();

        Assert.Equal(modern2x, locator.TryLocate(_root, "1874880", GameMediaAssetType.Cover));

        File.Delete(modern2x);
        Assert.Equal(modern, locator.TryLocate(_root, "1874880", GameMediaAssetType.Cover));

        File.Delete(modern);
        Assert.Equal(legacy2x, locator.TryLocate(_root, "1874880", GameMediaAssetType.Cover));

        File.Delete(legacy2x);
        Assert.Equal(legacy, locator.TryLocate(_root, "1874880", GameMediaAssetType.Cover));
    }

    [Fact]
    public void TryLocate_prefers_the_appinfo_hash_directory_when_supplied()
    {
        var older = CreateNestedAsset("3768760", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "library_600x900_2x.jpg");
        var preferred = CreateNestedAsset("3768760", "86d898447e0e475e3f8a9cc1ef660a80032472d7", "library_600x900_2x.jpg");
        var locator = new SteamLocalMediaLocator();

        Assert.Equal(
            preferred,
            locator.TryLocate(
                _root,
                "3768760",
                GameMediaAssetType.Cover,
                "86d898447e0e475e3f8a9cc1ef660a80032472d7"));
        Assert.NotEqual(older, preferred);
    }

    [Fact]
    public void TryLocate_preserves_direct_then_flat_legacy_then_sorted_hash_priority()
    {
        var direct = CreateLocalAsset("1874880", "library_hero.jpg");
        var legacy = CreateLegacyAsset("1874880", "library_hero.jpg");
        var nestedLater = CreateNestedAsset(
            "1874880",
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            "library_hero.jpg");
        var nestedFirst = CreateNestedAsset(
            "1874880",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            "library_hero.jpg");
        var locator = new SteamLocalMediaLocator();

        Assert.Equal(direct, locator.TryLocate(_root, "1874880", GameMediaAssetType.Hero));

        File.Delete(direct);
        Assert.Equal(legacy, locator.TryLocate(_root, "1874880", GameMediaAssetType.Hero));

        File.Delete(legacy);
        Assert.Equal(nestedFirst, locator.TryLocate(_root, "1874880", GameMediaAssetType.Hero));
        Assert.NotEqual(nestedLater, nestedFirst);
    }

    [Fact]
    public void TryLocate_never_uses_another_AppId_or_unrelated_nested_file()
    {
        CreateNestedAsset(
            "2116120",
            "5c3871f559599645d024432ff4e791a234c53976",
            "library_hero.jpg");
        CreateNestedAsset(
            "1203620",
            "18580ba964928c55f96c4d46809d4fa3aa7698d1",
            "custom_hero.jpg");
        var locator = new SteamLocalMediaLocator();

        Assert.Null(locator.TryLocate(_root, "1203620", GameMediaAssetType.Hero));
    }

    [Fact]
    public void TryLocate_rejects_path_traversal_even_when_target_exists()
    {
        var escapedDirectory = Path.Combine(_root, "appcache", "2116120");
        Directory.CreateDirectory(escapedDirectory);
        File.WriteAllBytes(
            Path.Combine(escapedDirectory, "library_hero.jpg"),
            [1]);
        var locator = new SteamLocalMediaLocator();

        Assert.Null(locator.TryLocate(
            _root,
            "../2116120",
            GameMediaAssetType.Hero));
    }

    [Fact]
    public void TryLocate_rejects_non_numeric_AppId()
    {
        var locator = new SteamLocalMediaLocator();

        Assert.Null(locator.TryLocate(
            _root,
            "not-an-app-id",
            GameMediaAssetType.Hero));
    }

    [Fact]
    public void TryLocate_ignores_non_hash_nested_directories()
    {
        CreateNestedAsset("2116120", "not-a-steam-hash", "library_hero.jpg");
        var locator = new SteamLocalMediaLocator();

        Assert.Null(locator.TryLocate(_root, "2116120", GameMediaAssetType.Hero));
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

    private string CreateLocalAsset(string appId, string filename)
    {
        var directory = Path.Combine(_root, "appcache", "librarycache");
        directory = Path.Combine(directory, appId);
        Directory.CreateDirectory(directory);
        var path = Path.GetFullPath(Path.Combine(directory, filename));
        // The locator checks file existence, not image decoding.
        File.WriteAllBytes(path, [1]);
        return path;
    }

    private string CreateLegacyAsset(string appId, string filename)
    {
        var directory = Path.Combine(_root, "appcache", "librarycache");
        Directory.CreateDirectory(directory);
        var path = Path.GetFullPath(Path.Combine(directory, $"{appId}_{filename}"));
        File.WriteAllBytes(path, [1]);
        return path;
    }

    private string CreateNestedAsset(
        string appId,
        string hash,
        string filename)
    {
        var directory = Path.Combine(
            _root,
            "appcache",
            "librarycache",
            appId,
            hash);
        Directory.CreateDirectory(directory);
        var path = Path.GetFullPath(Path.Combine(directory, filename));
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
