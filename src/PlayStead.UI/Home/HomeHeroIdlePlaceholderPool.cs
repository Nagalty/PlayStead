using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PlayStead.UI.Home;

internal static class HomeHeroIdlePlaceholderPool
{
    internal const string EditorialPlaceholderResourcePath = "Assets/Home/HomeHeroIdle01.png";
    internal const string DormantPlaceholderResourcePath = "Assets/Home/HomeHeroIdle02.png";

    private static readonly IReadOnlyList<string> ResourcePathsInternal = Array.AsReadOnly(
    [
        EditorialPlaceholderResourcePath,
        "Assets/Home/HomeHeroIdle02.png",
        "Assets/Home/HomeHeroIdle03.png",
        "Assets/Home/HomeHeroIdle04.png"
    ]);

    internal static IReadOnlyList<string> ResourcePaths => ResourcePathsInternal;

    internal static string SelectPath(Random random) =>
        ResourcePathsInternal[random.Next(ResourcePathsInternal.Count)];

    internal static ImageSource? TryLoad(string resourcePath)
    {
        try
        {
            var resource = Application.GetResourceStream(
                new Uri($"/PlayStead.UI;component/{resourcePath}", UriKind.Relative));
            if (resource is null)
            {
                return null;
            }

            using var stream = resource.Stream;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
