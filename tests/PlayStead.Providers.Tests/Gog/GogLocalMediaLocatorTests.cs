using PlayStead.Core.Media;
using PlayStead.Providers.Gog;

namespace PlayStead.Providers.Tests.Gog;

public sealed class GogLocalMediaLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead-GogMedia-" + Guid.NewGuid().ToString("N"));

    public GogLocalMediaLocatorTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Exact_product_id_finds_vertical_cover_and_hero()
    {
        var cache = CreateCache("1495134320");
        CopyFixture("gog_vertical_cover.webp", Path.Combine(cache, "hash_glx_vertical_cover.webp"));
        CopyFixture("gog_hero.webp", Path.Combine(cache, "hash_glx_bg_top_padding_7.webp"));

        var locator = new GogLocalMediaLocator(_root);

        Assert.EndsWith("_glx_vertical_cover.webp", locator.TryLocate("1495134320", GameMediaAssetType.Cover));
        Assert.EndsWith("_glx_bg_top_padding_7.webp", locator.TryLocate("1495134320", GameMediaAssetType.Hero));
    }

    [Fact]
    public void Other_product_id_is_never_used()
    {
        var cache = CreateCache("1495134321");
        CopyFixture("gog_vertical_cover.webp", Path.Combine(cache, "hash_glx_vertical_cover.webp"));

        Assert.Null(new GogLocalMediaLocator(_root).TryLocate("1495134320", GameMediaAssetType.Cover));
    }

    [Fact]
    public void Square_icon_does_not_substitute_for_cover()
    {
        var cache = CreateCache("1495134320");
        CopyFixture("gog_square_icon.webp", Path.Combine(cache, "hash_glx_square_icon_v2.webp"));

        Assert.Null(new GogLocalMediaLocator(_root).TryLocate("1495134320", GameMediaAssetType.Cover));
    }

    [Fact]
    public void Missing_webcache_returns_null()
    {
        var missing = Path.Combine(_root, "missing");

        Assert.Null(new GogLocalMediaLocator(missing).TryLocate("1495134320", GameMediaAssetType.Cover));
    }

    [Fact]
    public void Non_numeric_product_id_returns_null()
    {
        Assert.Null(new GogLocalMediaLocator(_root).TryLocate("witcher3", GameMediaAssetType.Cover));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private string CreateCache(string productId)
    {
        var path = Path.Combine(_root, "cache-instance", "gog", productId);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CopyFixture(string name, string destination)
    {
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gog", name),
            destination);
    }
}
