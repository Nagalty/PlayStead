using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailViewContractTests
{
    [Fact]
    public void Game_detail_view_binds_only_real_library_metadata()
    {
        var xaml =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "GameDetailView.xaml")));

        Assert.NotNull(
            FindBinding(
                xaml,
                "Title"));

        Assert.NotNull(
            FindBinding(
                xaml,
                "ProviderLabel"));

        Assert.NotNull(
            FindBinding(
                xaml,
                "InstallPath"));

        Assert.NotNull(
            FindBinding(
                xaml,
                "InstalledSizeLabel"));

        Assert.NotNull(
            FindBinding(
                xaml,
                "SteamStatusLabel"));

        Assert.NotNull(
            FindBinding(
                xaml,
                "SessionStatusLabel"));
    }

    [Fact]
    public void MainWindow_renders_GameDetail_from_current_GameId_parameter()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "case AppRoute.GameDetail:",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "CurrentParameter is not",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "GameId",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "Items.FirstOrDefault",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "new GameDetailViewModel",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "new GameDetailView",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "MainContent.Content =",
            source,
            StringComparison.Ordinal);
    }

    private static XElement? FindBinding(
        XDocument document,
        string propertyName)
    {
        return document
            .Descendants()
            .FirstOrDefault(
                element =>
                    element
                        .Attributes()
                        .Any(
                            attribute =>
                                attribute.Value.Contains(
                                    $"{{Binding {propertyName}",
                                    StringComparison.Ordinal)));
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
