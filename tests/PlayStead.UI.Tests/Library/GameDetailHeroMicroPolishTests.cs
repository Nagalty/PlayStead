using System.Xml.Linq;
using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailHeroMicroPolishTests
{
    private static XDocument View => XDocument.Load(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));

    [Fact]
    public void Content_rail_starts_at_hero_edge_and_remains_local()
    {
        var rail = View.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "HeroContentRail"));

        Assert.Equal("0", (string?)rail.Attribute("Margin"));
        Assert.Null(rail.Attribute("Width"));
        Assert.Equal("Transparent", (string?)rail.Attribute("Background"));

        var hero = View.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "GameDetailHero"));
        Assert.Equal("0", (string?)hero.Attribute("Padding"));
    }

}
