using PlayStead.Core.Media;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaUriFactoryTests
{
    [Theory]
    [InlineData(
        GameMediaAssetType.Cover,
        "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg")]
    public void CreateCandidates_uses_exact_AppId_asset_URL(
        GameMediaAssetType type,
        string expected)
    {
        var uri = Assert.Single(
            SteamMediaUriFactory.CreateCandidates(
                "1874880",
                type));

        Assert.Equal(
            expected,
            uri.AbsoluteUri);
    }

    [Theory]
    [InlineData(GameMediaAssetType.Header)]
    [InlineData(GameMediaAssetType.Hero)]
    [InlineData(GameMediaAssetType.Logo)]
    public void CreateCandidates_throws_for_asset_types_not_supported_in_Task3(
        GameMediaAssetType type)
    {
        Assert.Throws<NotSupportedException>(() =>
            SteamMediaUriFactory.CreateCandidates(
                "1874880",
                type));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("1874880x")]
    [InlineData("-1")]
    public void CreateCandidates_rejects_non_numeric_AppId(
        string appId)
    {
        Assert.Throws<ArgumentException>(() =>
            SteamMediaUriFactory.CreateCandidates(
                appId,
                GameMediaAssetType.Cover));
    }
}
