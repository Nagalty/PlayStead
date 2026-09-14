namespace PlayStead.UI.Tests.Library;

public sealed class MainWindowGameDetailNavigationContractTests
{
    [Fact]
    public void MainWindow_subscribes_to_library_game_detail_requests()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "_libraryView.GameDetailsRequested +=",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "LibraryView_OnGameDetailsRequested",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Library_game_detail_request_uses_selected_game_and_navigation_service()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "GameQuickPanelViewModel",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "SelectedItem",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "OpenDetails()",
            source,
            StringComparison.Ordinal);
    }

    private static string FindUiFile(
        string relativePath)
    {
        var directory =
            new DirectoryInfo(
                AppContext.BaseDirectory);

        while (directory is not null)
        {
            var uiDirectory =
                Path.Combine(
                    directory.FullName,
                    "src",
                    "PlayStead.UI");

            if (Directory.Exists(
                    uiDirectory))
            {
                var path =
                    Path.Combine(
                        uiDirectory,
                        relativePath);

                Assert.True(
                    File.Exists(
                        path),
                    $"Required UI file missing: {relativePath}");

                return path;
            }

            directory =
                directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "PlayStead.UI source directory was not found.");
    }
}
