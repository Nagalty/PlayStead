using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryDoubleClickInteractionTests
{
    [Fact]
    public void GameArtwork_is_the_only_card_element_with_double_click_navigation()
    {
        var xaml = XDocument.Load(Find("Controls/GameCard.xaml"));
        var artwork = xaml.Descendants().Single(e => e.Name.LocalName == "GameArtwork");
        Assert.Equal("GameArtwork_OnMouseDoubleClick", artwork.Attribute("MouseDoubleClick")?.Value);
        Assert.DoesNotContain(xaml.Descendants(), e => e.Attribute("MouseDoubleClick") is not null && e != artwork);
    }

    [Fact]
    public void Library_wires_cover_double_click_to_the_existing_detail_request_path()
    {
        var xaml = File.ReadAllText(Find("Library/LibraryView.xaml"));
        var code = File.ReadAllText(Find("Library/LibraryView.xaml.cs"));
        Assert.Contains("DetailsRequested=\"GameCard_OnDetailsRequested\"", xaml, StringComparison.Ordinal);
        Assert.Contains("GameDetailsRequested?.Invoke", code, StringComparison.Ordinal);
        Assert.Contains("GameCard_OnDetailsRequested", code, StringComparison.Ordinal);
    }

    private static string Find(string relative)
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var p = Path.Combine(d.FullName, "src", "PlayStead.UI", relative);
            if (File.Exists(p)) return p;
            d = d.Parent;
        }
        throw new FileNotFoundException(relative);
    }
}
