using System.Xml.Linq;
using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailHeroReadabilityPanelTests
{
    private static XDocument View => XDocument.Load(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));

    [Fact]
    public void Hero_uses_a_local_high_contrast_content_rail()
    {
        var rail = View.Descendants()
            .Single(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "HeroContentRail"));

        Assert.Equal("Left", (string?)rail.Attribute("HorizontalAlignment"));
        Assert.Equal("520", (string?)rail.Attribute("Width"));
        Assert.Equal("0.86", (string?)rail.Attribute("Opacity"));
        Assert.Equal(
            "{DynamicResource PlayStead.Brush.SurfaceStrong}",
            (string?)rail.Attribute("Background"));
        Assert.Contains(rail.Descendants(), element =>
            element.Name.LocalName == "LinearGradientBrush");
    }

    [Fact]
    public void Hero_activity_uses_distinct_label_and_value_groups()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));

        Assert.Contains("Text=\"Temps joué\"", source, StringComparison.Ordinal);
        Assert.Contains("Text=\"Dernière partie\"", source, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Activity.TotalPlayTimeLabel}\"", source, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Activity.LastActivityLabel}\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Dernière utilisation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_has_no_global_veil()
    {
        var hero = View.Descendants()
            .Single(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "GameDetailHero"));
        var rails = hero.Descendants()
            .Where(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "HeroContentRail"))
            .ToArray();

        Assert.Single(rails);
        Assert.Equal("Left", (string?)rails[0].Attribute("HorizontalAlignment"));
    }
}
