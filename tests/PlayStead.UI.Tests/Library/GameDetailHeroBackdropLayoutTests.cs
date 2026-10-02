using Xunit;
using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailHeroBackdropLayoutTests
{
    private static string View => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));

    [Fact]
    public void Hero_is_a_full_backdrop_without_card_chrome()
    {
        var hero = View.Split("x:Name=\"GameDetailHero\"", 2)[1].Split("</Border>", 2)[0];
        Assert.DoesNotContain("BorderThickness=\"1\"", hero, StringComparison.Ordinal);
        Assert.DoesNotContain("CornerRadius=", hero, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_is_full_bleed_while_content_cards_keep_their_inset()
    {
        var document = XDocument.Parse(View);
        var hero = document.Descendants()
            .Single(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "GameDetailHero"));
        var rootLayout = hero.Parent!;
        var detailColumns = document.Descendants()
            .Single(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "DetailColumns"));
        var contentInset = detailColumns.Parent!;

        Assert.Null(rootLayout.Attribute("Margin"));
        Assert.Null(hero.Attribute("Margin"));
        Assert.Equal(
            "{DynamicResource PlayStead.Spacing.4}",
            (string?)contentInset.Attribute("Margin"));
        Assert.Contains("MinHeight=\"320\"", View, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_media_and_local_gradient_are_preserved()
    {
        Assert.Contains("{Binding HeroPath}", View, StringComparison.Ordinal);
        Assert.Contains("Width=\"600\"", View, StringComparison.Ordinal);
        Assert.Contains("OpacityMask", View, StringComparison.Ordinal);
        Assert.Contains("PlaySplitButton", View, StringComparison.Ordinal);
    }

    [Fact]
    public void Hero_scrim_protects_text_with_a_dark_to_transparent_gradient()
    {
        var document = XDocument.Parse(View);
        var hero = document.Descendants()
            .Single(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" && attribute.Value == "GameDetailHero"));
        var children = hero.Descendants().ToList();
        var railIndex = children.FindIndex(element => element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "Name" && attribute.Value == "HeroContentRail"));
        var contentIndex = children.FindIndex(element => element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "Name" && attribute.Value == "HeroContent"));
        var rail = children[railIndex];

        Assert.True(railIndex >= 0 && contentIndex > railIndex);
        Assert.True(double.Parse((string?)rail.Attribute("Opacity") ?? "0", System.Globalization.CultureInfo.InvariantCulture) >= 0.9);
        var stops = rail.Descendants().Where(element => element.Name.LocalName == "GradientStop").ToArray();
        Assert.True(stops.Length >= 3);
        Assert.Contains(stops, stop =>
            string.Equals((string?)stop.Attribute("Offset"), "0", StringComparison.Ordinal) &&
            !string.Equals((string?)stop.Attribute("Color"), "#00000000", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(stops, stop =>
            string.Equals((string?)stop.Attribute("Color"), "#00000000", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Steam", rail.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GOG", rail.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Manual", rail.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
