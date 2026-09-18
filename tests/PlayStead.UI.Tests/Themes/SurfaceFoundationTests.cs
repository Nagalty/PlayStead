using System.Xml.Linq;

namespace PlayStead.UI.Tests.Themes;

public sealed class SurfaceFoundationTests
{
    [Fact]
    public void Surface_roles_and_status_roles_are_centralized()
    {
        var controls = Load("src/PlayStead.UI/Themes/PlaySteadControls.xaml");
        var keys = controls.Descendants(XName.Get("Style", "http://schemas.microsoft.com/winfx/2006/xaml/presentation"))
            .Select(style => (string?)style.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")))
            .Where(key => key is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("PlayStead.Surface.Panel", keys);
        Assert.Contains("PlayStead.Surface.Card", keys);
        Assert.Contains("PlayStead.Surface.CardNested", keys);
        Assert.Contains("PlayStead.Surface.Metric", keys);
        Assert.Contains("PlayStead.Surface.Selected", keys);
        Assert.Contains("PlayStead.Status.Badge.Neutral", keys);
        Assert.Contains("PlayStead.Status.Badge.Success", keys);
        Assert.Contains("PlayStead.Status.Badge.Warning", keys);
        Assert.Contains("PlayStead.Status.Badge.Error", keys);
        Assert.Contains("PlayStead.Status.Badge.Info", keys);
    }

    [Fact]
    public void Representative_controls_consume_shared_surface_roles()
    {
        var card = Load("src/PlayStead.UI/Controls/GameCard.xaml");
        var badge = Load("src/PlayStead.UI/Controls/StatusBadge.xaml");

        Assert.Contains("PlayStead.Surface.Card", card.ToString(SaveOptions.DisableFormatting));
        Assert.Contains("PlayStead.Status.Badge.Neutral", badge.ToString(SaveOptions.DisableFormatting));
    }

    private static XElement Load(string relative)
    {
        var root = FindRoot();
        return XElement.Load(Path.Combine(root, relative));
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
