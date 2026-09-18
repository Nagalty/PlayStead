using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class LibrarySearchUiContractTests
{
    [Fact]
    public void Library_view_has_no_local_search_control_and_filters_list_projection()
    {
        var xaml =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        Assert.DoesNotContain(
            xaml.Descendants("TextBox"),
            element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "Name" &&
                attribute.Value.Contains("Search", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(
            xaml.Descendants(),
            element => element.Attribute("Text")?.Value.Contains("Rechercher un jeu", StringComparison.OrdinalIgnoreCase) == true);

        var gameList =
            FindRequiredByName(
                xaml,
                "GameList");

        Assert.Equal(
            "{Binding VisibleItems}",
            gameList.Attribute(
                "ItemsSource")?.Value);
    }

    [Fact]
    public void Library_view_forwards_search_text_and_exposes_focus_entry_point()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml.cs")));

        Assert.Contains(
            "public void FocusSearch()",
            source,
            StringComparison.Ordinal);
        Assert.Contains("SearchQuery", File.ReadAllText(FindUiFile(Path.Combine("Library", "LibraryViewModel.cs"))), StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_handles_control_k_as_global_library_search_shortcut()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    "MainWindow.xaml.cs"));

        Assert.Contains(
            "Key.K",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "ModifierKeys.Control",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "AppRoute.Library",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "ShellSearchBox.Focus()",
            source,
            StringComparison.Ordinal);
    }

    private static XElement FindRequiredByName(
        XDocument document,
        string name)
    {
        return document
                   .Descendants()
                   .SingleOrDefault(
                       element =>
                           element
                               .Attributes()
                               .Any(
                                   attribute =>
                                       attribute.Name.LocalName ==
                                           "Name" &&
                                       attribute.Value ==
                                           name))
               ?? throw new Xunit.Sdk.XunitException(
                   $"Element with x:Name '{name}' was not found.");
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
