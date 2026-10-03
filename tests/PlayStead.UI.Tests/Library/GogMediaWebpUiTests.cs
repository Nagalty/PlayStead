using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Data.Media;
using PlayStead.Providers.Gog;
using PlayStead.UI.Tests.TestSupport;

namespace PlayStead.UI.Tests.Library;

public sealed class GogMediaWebpUiTests
{
    [Fact]
    public async Task Gog_provider_resolver_cache_and_projection_round_trip_WebP_without_Wic()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead-GogWebp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "galaxy", "cache-instance", "gog", "1495134320"));
        try
        {
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "Gog", "gog_vertical_cover.webp"),
                Path.Combine(root, "galaxy", "cache-instance", "gog", "1495134320", "hash_glx_vertical_cover.webp"));

            var resolver = new GameMediaResolver(
                new FileGameMediaCache(Path.Combine(root, "playstead-cache")),
                [new GogMediaProvider(new GogLocalMediaLocator(Path.Combine(root, "galaxy")))]);
            var path = await resolver.ResolveAndCacheAsync(
                new GameMediaIdentity(ProviderKind.Gog, "1495134320", "The Witcher 3"),
                GameMediaAssetType.Cover,
                CancellationToken.None);

            Assert.NotNull(path);
            Assert.EndsWith("cover.webp", path, StringComparison.OrdinalIgnoreCase);

        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [WpfWebpFact]
    public void Wpf_decodes_GOG_WebP_fixture_for_current_image_projection()
    {
        PlaySteadWpfTestResources.Run(() =>
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "Gog",
                "gog_vertical_cover.webp");
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            Assert.True(image.PixelWidth > 0);
            Assert.True(image.PixelHeight > 0);
        });
    }
}

internal sealed class WpfWebpFactAttribute : FactAttribute
{
    public WpfWebpFactAttribute()
    {
        if (!WicWebpCapability.IsAvailable())
            Skip = "WIC WebP decoder is not available on this Windows environment.";
    }
}

internal static class WicWebpCapability
{
    public static bool IsAvailable()
    {
        var available = false;
        var thread = new Thread(() => available = TryDecode());
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return available;
    }

    private static bool TryDecode()
    {
        try
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "Gog",
                "gog_vertical_cover.webp");
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image.PixelWidth > 0 && image.PixelHeight > 0;
        }
        catch (NotSupportedException exception) when (IsMissingWicWebpDecoder(exception))
        {
            return false;
        }
        catch (COMException exception) when (exception.HResult == unchecked((int)0x88982F8B))
        {
            return false;
        }
    }

    private static bool IsMissingWicWebpDecoder(NotSupportedException exception) =>
        exception.Message.Contains("imaging component", StringComparison.OrdinalIgnoreCase) ||
        exception.InnerException is COMException { HResult: unchecked((int)0x88982F8B) };
}
