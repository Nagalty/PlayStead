using Xunit;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailHeroMetadataTests
{
    private static string ViewModelSource => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Library", "GameDetailViewModel.cs"));

    private static string ViewSource => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PlayStead.UI", "Library", "GameDetailView.xaml"));

    [Fact]
    public void Hero_uses_presentation_title_without_mutating_source_title()
    {
        Assert.Contains("public string DisplayTitle", ViewModelSource, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding DisplayTitle}\"", ViewSource, StringComparison.Ordinal);
        Assert.Contains("game.Title.ToUpperInvariant()", ViewModelSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Activity_metadata_are_distinct_and_readable()
    {
        Assert.Contains("Text=\"{Binding Activity.TotalPlayTimeLabel}\"", ViewSource, StringComparison.Ordinal);
        Assert.Contains("Text=\"Dernière partie\"", ViewSource, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Activity.LastActivityLabel}\"", ViewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\" jouées\"", ViewSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Margin=\"0,0,{DynamicResource", ViewSource, StringComparison.Ordinal);
        Assert.Contains("<UniformGrid Margin=\"0,12,0,0\" Columns=\"3\">", ViewSource, StringComparison.Ordinal);
        Assert.Contains("Text=\"Temps joué\"", ViewSource, StringComparison.Ordinal);
        Assert.Contains("Text=\"Dernière partie\"", ViewSource, StringComparison.Ordinal);
        Assert.Contains("Activity.TotalPlayTimeLabel", ViewSource, StringComparison.Ordinal);
        Assert.Contains("Activity.LastActivityLabel", ViewSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Session_status_remains_inline_and_conditional()
    {
        Assert.Contains("Text=\"{Binding SessionStatusLabel}\"", ViewSource, StringComparison.Ordinal);
        Assert.Contains("Visibility=\"{Binding Game.IsSessionActive", ViewSource, StringComparison.Ordinal);
    }
}
