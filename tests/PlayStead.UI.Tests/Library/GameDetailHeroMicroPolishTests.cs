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
        Assert.Equal("520", (string?)rail.Attribute("Width"));
        Assert.Contains(rail.Descendants(), element =>
            element.Name.LocalName == "LinearGradientBrush");

        var hero = View.Descendants().Single(element =>
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "GameDetailHero"));
        Assert.Equal("0", (string?)hero.Attribute("Padding"));
    }

    [Fact]
    public void Cover_uses_larger_clipped_rounded_wrapper_without_border()
    {
        var artwork = View.Descendants().Single(element =>
            element.Name.LocalName == "GameArtwork" &&
            element.Attributes().Any(attribute => attribute.Name.LocalName == "SourcePath"));
        var wrapper = artwork.Parent!;

        Assert.Equal("176", (string?)wrapper.Attribute("Width"));
        Assert.Equal("264", (string?)wrapper.Attribute("Height"));
        Assert.Equal("12", (string?)wrapper.Attribute("CornerRadius"));
        Assert.Equal("True", (string?)wrapper.Attribute("ClipToBounds"));
        Assert.Null(wrapper.Attribute("BorderBrush"));
        Assert.Equal("{Binding CoverPath}", (string?)artwork.Attribute("SourcePath"));

        Assert.Equal("24,24,0,24", (string?)wrapper.Attribute("Margin"));

        var geometry = artwork.Descendants().Single(element =>
            element.Name.LocalName == "RectangleGeometry");
        Assert.Equal("0,0,176,264", (string?)geometry.Attribute("Rect"));
        Assert.Equal("12", (string?)geometry.Attribute("RadiusX"));
        Assert.Equal("12", (string?)geometry.Attribute("RadiusY"));
    }
}
