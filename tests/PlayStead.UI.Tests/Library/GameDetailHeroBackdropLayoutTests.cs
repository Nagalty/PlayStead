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
        Assert.Contains("Width=\"520\"", View, StringComparison.Ordinal);
        Assert.Contains("OpacityMask", View, StringComparison.Ordinal);
        Assert.Contains("PlaySplitButton", View, StringComparison.Ordinal);
    }
}
