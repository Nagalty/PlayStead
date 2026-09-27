namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailBuildHistoryStructureTests
{
    [Fact]
    public void Build_history_section_renders_summary_and_timeline_projection()
    {
        var path = Path.Combine(FindRoot(), "src", "PlayStead.UI", "Library", "GameDetailView.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("x:Name=\"BuildHistorySection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Historique des changements", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding BuildHistorySummary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding BuildHistory}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding ObservedAtLabel}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding BuildTransitionLabel}", xaml, StringComparison.Ordinal);
        Assert.Contains("{Binding SinceLastPlayLabel}", xaml, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
