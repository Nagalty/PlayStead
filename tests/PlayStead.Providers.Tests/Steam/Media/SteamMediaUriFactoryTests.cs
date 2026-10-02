using PlayStead.Core.Media;
using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Media;

namespace PlayStead.Providers.Tests.Steam.Media;

public sealed class SteamMediaUriFactoryTests
{
    [Theory]
    [InlineData(
        GameMediaAssetType.Hero,
        "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_hero.jpg")]
    [InlineData(
        GameMediaAssetType.Logo,
        "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/logo.png")]
    public void CreateCandidates_uses_exact_AppId_asset_URL(
        GameMediaAssetType type,
        string expected)
    {
        var candidates = SteamMediaUriFactory.CreateCandidates("1874880", type);
        Assert.Contains(expected, candidates.Select(uri => uri.AbsoluteUri));
    }

    [Fact]
    public void CreateCandidates_Cover_includes_official_capsule_variants_before_legacy_fallbacks()
    {
        Assert.Collection(
            SteamMediaUriFactory.CreateCandidates("1874880", GameMediaAssetType.Cover),
            uri => Assert.Equal("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_capsule_2x.jpg", uri.AbsoluteUri),
            uri => Assert.Equal("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_capsule.jpg", uri.AbsoluteUri),
            uri => Assert.Equal("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900_2x.jpg", uri.AbsoluteUri),
            uri => Assert.Equal("https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_600x900.jpg", uri.AbsoluteUri));
    }

    [Fact]
    public void CreateCandidates_Cover_uses_official_hash_for_modern_store_item_assets()
    {
        const string hash = "86d898447e0e475e3f8a9cc1ef660a80032472d7";
        var candidates = SteamMediaUriFactory.CreateCandidates(
            "3768760",
            new SteamMediaAssetMetadata(hash, hash),
            GameMediaAssetType.Cover);

        Assert.Equal(
            "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/3768760/86d898447e0e475e3f8a9cc1ef660a80032472d7/library_600x900_2x.jpg",
            candidates[0].AbsoluteUri);
        Assert.Contains(
            candidates,
            uri => uri.AbsoluteUri.EndsWith("/library_600x900.jpg", StringComparison.Ordinal));
        Assert.Contains(
            candidates,
            uri => uri.AbsoluteUri.Contains("cdn.cloudflare.steamstatic.com/steam/apps/3768760", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateCandidates_Hero_uses_official_hash_and_filename_before_legacy_fallback()
    {
        const string hash = "86d898447e0e475e3f8a9cc1ef660a80032472d7";
        var candidates = SteamMediaUriFactory.CreateCandidates(
            "3768760",
            new SteamMediaAssetMetadata(
                null,
                null,
                null,
                hash,
                [new SteamMediaAssetReference(hash, "library_hero_2x.jpg")]),
            GameMediaAssetType.Hero);

        Assert.Equal(
            "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/3768760/86d898447e0e475e3f8a9cc1ef660a80032472d7/library_hero_2x.jpg",
            candidates[0].AbsoluteUri);
        Assert.Contains(
            candidates,
            uri => uri.AbsoluteUri == "https://cdn.cloudflare.steamstatic.com/steam/apps/3768760/library_hero.jpg");
    }

    [Fact]
    public void CreateCandidates_Header_returns_exact_candidates_in_priority_order()
    {
        var candidates = SteamMediaUriFactory.CreateCandidates(
            "1874880",
            GameMediaAssetType.Header);

        Assert.Collection(
            candidates,
            uri => Assert.Equal(
                "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/library_header.jpg",
                uri.AbsoluteUri),
            uri => Assert.Equal(
                "https://cdn.cloudflare.steamstatic.com/steam/apps/1874880/header.jpg",
                uri.AbsoluteUri));
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
