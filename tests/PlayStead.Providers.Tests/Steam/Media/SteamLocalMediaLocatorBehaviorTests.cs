using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamLocalMediaLocatorBehaviorTests : IDisposable
{
    private readonly string _root =
        Path.Combine(
            Path.GetTempPath(),
            "PlayStead.Tests",
            Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryLocate_returns_exact_cover_path_for_AppId()
    {
        var libraryCache = Path.Combine(
            _root,
            "appcache",
            "librarycache");

        Directory.CreateDirectory(libraryCache);

        var expected = Path.Combine(
            libraryCache,
            "1874880_library_600x900.jpg");

        File.WriteAllBytes(
            expected,
            [0xFF, 0xD8, 0xFF, 0xD9]);

        var locator = new SteamLocalMediaLocator();

        var actual = locator.TryLocate(
            _root,
            "1874880",
            GameMediaAssetType.Cover);

        Assert.Equal(
            Path.GetFullPath(expected),
            actual);
    }

    [Fact]
    public void TryLocate_does_not_match_partial_AppId()
    {
        var libraryCache = Path.Combine(
            _root,
            "appcache",
            "librarycache");

        Directory.CreateDirectory(libraryCache);

        File.WriteAllBytes(
            Path.Combine(
                libraryCache,
                "1874880_library_600x900.jpg"),
            [0xFF, 0xD8, 0xFF, 0xD9]);

        var locator = new SteamLocalMediaLocator();

        var actual = locator.TryLocate(
            _root,
            "18748",
            GameMediaAssetType.Cover);

        Assert.Null(actual);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }
}
