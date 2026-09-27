namespace PlayStead.UI.Tests.Shell;

public sealed class GlobalSearchStructureTests
{
    [Fact]
    public void MainWindow_declares_live_search_popup_and_result_navigation()
    {
        var path = Path.Combine(FindRoot(), "src", "PlayStead.UI", "MainWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.Contains("x:Name=\"GlobalSearchPopup\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsOpen=\"{Binding Search.IsOpen, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Search.Results}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Search.SelectResultCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("Aucun jeu trouvé", xaml, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PlayStead.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("PlayStead root not found.");
    }
}
