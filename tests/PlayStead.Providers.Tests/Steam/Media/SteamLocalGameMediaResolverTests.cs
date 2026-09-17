using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamLocalGameMediaResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryGetPath_returns_local_steam_asset_without_network()
    {
        var directory = Path.Combine(
            _root,
            "appcache",
            "librarycache",
            "1874880");
        Directory.CreateDirectory(directory);
        var expected = Path.Combine(directory, "library_hero.jpg");
        File.WriteAllBytes(expected, [1]);

        var resolver = new SteamLocalGameMediaResolver(
            new WindowsSteamRootLocator([_root]),
            new SteamLocalMediaLocator());

        var actual = resolver.TryGetPath(
            new GameMediaIdentity(ProviderKind.Steam, "1874880", "Arma Reforger"),
            GameMediaAssetType.Hero);

        Assert.Equal(Path.GetFullPath(expected), actual);
    }

    [Fact]
    public void TryGetPath_returns_null_for_unsupported_provider()
    {
        var resolver = new SteamLocalGameMediaResolver(
            new WindowsSteamRootLocator([_root]),
            new SteamLocalMediaLocator());

        var actual = resolver.TryGetPath(
            new GameMediaIdentity(ProviderKind.Epic, "game", "Game"),
            GameMediaAssetType.Hero);

        Assert.Null(actual);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
