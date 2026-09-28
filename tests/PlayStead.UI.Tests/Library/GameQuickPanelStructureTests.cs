namespace PlayStead.UI.Tests.Library;

public sealed class GameQuickPanelStructureTests
{
    [Fact]
    public void Quick_panel_has_conditional_attention_badge_and_since_last_play_summary()
    {
        var path = Path.Combine(FindRoot(), "src", "PlayStead.UI", "Library", "LibraryView.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("Text=\"À SIGNALER\"", xaml, StringComparison.Ordinal);
        Assert.Contains("QuickPanelViewModel.HasAttention", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"Depuis ta dernière partie\"", xaml, StringComparison.Ordinal);
        Assert.Contains("QuickPanelViewModel.HasSinceLastPlaySummary", xaml, StringComparison.Ordinal);
        Assert.Contains("QuickPanelViewModel.SinceLastPlaySummary", xaml, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
