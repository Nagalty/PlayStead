using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Providers.Gog;

namespace PlayStead.Providers.Tests.Gog;

public sealed class GogMediaProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead-GogProvider-" + Guid.NewGuid().ToString("N"));

    public GogMediaProviderTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(GameMediaAssetType.Cover, "_glx_vertical_cover.webp")]
    [InlineData(GameMediaAssetType.Hero, "_glx_bg_top_padding_7.webp")]
    public async Task Gog_identity_returns_local_webp_payload(
        GameMediaAssetType assetType,
        string suffix)
    {
        var path = Path.Combine(_root, "cache", "cache-instance", "gog", "1495134320");
        Directory.CreateDirectory(path);
        var source = assetType switch
        {
            GameMediaAssetType.Cover => "gog_vertical_cover.webp",
            GameMediaAssetType.Hero => "gog_hero.webp",
            _ => "gog_hero.webp"
        };
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gog", source),
            Path.Combine(path, "hash" + suffix));

        var provider = new GogMediaProvider(new GogLocalMediaLocator(Path.Combine(_root, "cache")));
        var payload = await provider.ResolveAsync(
            new GameMediaIdentity(ProviderKind.Gog, "1495134320", "The Witcher 3"),
            assetType,
            CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal(assetType, payload.AssetType);
        Assert.Equal("1495134320", payload.ExternalId);
        Assert.Equal("image/webp", payload.ContentType);
        Assert.NotEmpty(payload.Content);
    }

    [Theory]
    [InlineData(ProviderKind.Steam)]
    [InlineData(ProviderKind.Manual)]
    public void Non_gog_identity_is_rejected(ProviderKind provider)
    {
        var subject = new GogMediaProvider(new GogLocalMediaLocator(_root));
        Assert.False(subject.CanResolve(new GameMediaIdentity(provider, provider == ProviderKind.Steam ? "123" : "manual:abc", "Game")));
    }

    [Fact]
    public async Task Non_numeric_product_id_returns_null()
    {
        var subject = new GogMediaProvider(new GogLocalMediaLocator(_root));
        var payload = await subject.ResolveAsync(
            new GameMediaIdentity(ProviderKind.Gog, "witcher3", "Game"),
            GameMediaAssetType.Cover,
            CancellationToken.None);
        Assert.Null(payload);
    }

    [Fact]
    public async Task Unsupported_asset_type_returns_null()
    {
        var subject = new GogMediaProvider(new GogLocalMediaLocator(_root));
        var payload = await subject.ResolveAsync(
            new GameMediaIdentity(ProviderKind.Gog, "1495134320", "Game"),
            GameMediaAssetType.Logo,
            CancellationToken.None);
        Assert.Null(payload);
    }

    [Fact]
    public async Task Square_icon_is_not_exposed_as_logo()
    {
        var path = Path.Combine(_root, "cache", "cache-instance", "gog", "1495134320");
        Directory.CreateDirectory(path);
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gog", "gog_square_icon.webp"),
            Path.Combine(path, "hash_glx_square_icon_v2.webp"));

        var provider = new GogMediaProvider(new GogLocalMediaLocator(Path.Combine(_root, "cache")));
        var payload = await provider.ResolveAsync(
            new GameMediaIdentity(ProviderKind.Gog, "1495134320", "The Witcher 3"),
            GameMediaAssetType.Logo,
            CancellationToken.None);

        Assert.Null(payload);
    }

    [Fact]
    public async Task Cancellation_is_propagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var subject = new GogMediaProvider(new GogLocalMediaLocator(_root));
        await Assert.ThrowsAsync<OperationCanceledException>(() => subject.ResolveAsync(
            new GameMediaIdentity(ProviderKind.Gog, "1495134320", "Game"),
            GameMediaAssetType.Cover,
            cancellation.Token));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
