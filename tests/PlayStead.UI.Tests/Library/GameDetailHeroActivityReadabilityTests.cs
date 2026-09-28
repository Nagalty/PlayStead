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
    public void Activity_line_remains_conditionally_visible()
    {
        Assert.Contains("Visibility=\"{Binding Activity.HasAnyActivity", Hero, StringComparison.Ordinal);
        Assert.Contains("BooleanToVisibilityConverter", Hero, StringComparison.Ordinal);
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
