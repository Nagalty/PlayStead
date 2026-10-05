using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailHeroActivityReadabilityTests
{
    private static string Hero => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));

    [Fact]
    public void Activity_line_uses_last_game_label()
    {
        Assert.Contains("Dernière partie", Hero, StringComparison.Ordinal);
        Assert.DoesNotContain("Dernière utilisation", Hero, StringComparison.Ordinal);
    }

    [Fact]
    public void Activity_values_use_readable_primary_semibold_text()
    {
        Assert.Contains("Text=\"{Binding Activity.TotalPlayTimeLabel}\"", Hero, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource PlayStead.Brush.TextPrimary}\"", Hero, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Activity.LastActivityLabel}\"", Hero, StringComparison.Ordinal);
    }

    [Fact]
    public void Activity_stats_use_two_compact_independent_surfaces()
    {
        var document = System.Xml.Linq.XDocument.Parse(Hero);
        var stats = document.Descendants()
            .Single(element =>
                element.Name.LocalName == "WrapPanel" &&
                ((string?)element.Attribute("Margin")) == "0,12,0,0");
        var surfaces = stats.Elements()
            .Where(element => element.Name.LocalName == "Border")
            .ToArray();

        Assert.Equal(2, surfaces.Length);
        Assert.All(surfaces, surface =>
        {
            Assert.Equal("6", (string?)surface.Attribute("CornerRadius"));
            Assert.Equal("{DynamicResource PlayStead.Brush.SurfaceStrong}", (string?)surface.Attribute("Background"));
            Assert.Equal("{DynamicResource PlayStead.Brush.Border}", (string?)surface.Attribute("BorderBrush"));
            Assert.Equal("1", (string?)surface.Attribute("BorderThickness"));
        });
        Assert.DoesNotContain("x:Name=\"HeroStatsBackplate\"", Hero, StringComparison.Ordinal);
    }

    [Fact]
    public void Activity_provider_details_remain_conditionally_visible()
    {
        Assert.Contains("Visibility=\"{Binding Activity.HasProviderActivity", Hero, StringComparison.Ordinal);
        Assert.Contains("BooleanToVisibilityConverter", Hero, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_provider_last_activity_row_is_collapsed_instead_of_showing_placeholder()
    {
        Assert.Contains("Visibility=\"{Binding Activity.HasProviderLastPlayed, Converter={StaticResource BooleanToVisibilityConverter}}\"", Hero, StringComparison.Ordinal);
        Assert.DoesNotContain("ProviderLastPlayedLabel, TargetNullValue=Inconnue", Hero, StringComparison.Ordinal);
    }

    [Fact]
    public void Active_status_remains_primary_semibold()
    {
        Assert.Contains(
            "PlayLabel=\"{Binding DataContext.SessionStatusLabel,",
            Hero,
            StringComparison.Ordinal);
        Assert.Contains(
            "TargetNullValue=Jouer",
            Hero,
            StringComparison.Ordinal);
        Assert.Contains(
            "controls:PlaySplitButton",
            Hero,
            StringComparison.Ordinal);
    }
}
