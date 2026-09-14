using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryResponsiveViewContractTests
{
    [Fact]
    public void Library_exposes_grid_and_list_toggle_buttons()
    {
        var document =
            LoadLibraryView();

        var gridButton =
            FindByName(
                document,
                "GridModeButton");

        var listButton =
            FindByName(
                document,
                "ListModeButton");

        Assert.Equal(
            "Grid",
            AttributeValue(
                gridButton,
                "Tag"));

        Assert.Equal(
            "List",
            AttributeValue(
                listButton,
                "Tag"));

        Assert.Equal(
            "GridModeButton_OnClick",
            AttributeValue(
                gridButton,
                "Click"));

        Assert.Equal(
            "ListModeButton_OnClick",
            AttributeValue(
                listButton,
                "Click"));
    }

    [Fact]
    public void Grid_and_list_hosts_follow_view_mode_flags()
    {
        var document =
            LoadLibraryView();

        var gridHost =
            FindByName(
                document,
                "GameGridRows");

        var listHost =
            FindByName(
                document,
                "GameList");

        Assert.Equal(
            "{Binding IsGridMode, Converter={StaticResource BooleanToVisibilityConverter}}",
            AttributeValue(
                gridHost,
                "Visibility"));

        Assert.Equal(
            "{Binding IsListMode, Converter={StaticResource BooleanToVisibilityConverter}}",
            AttributeValue(
                listHost,
                "Visibility"));
    }

    [Fact]
    public void Library_view_recomputes_grid_columns_when_size_changes()
    {
        var document =
            LoadLibraryView();

        var root =
            Assert.IsType<XElement>(
                document.Root);

        Assert.Equal(
            "LibraryView_OnSizeChanged",
            AttributeValue(
                root,
                "SizeChanged"));

        var codeBehind =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml.cs")));

        Assert.Contains(
            "LibraryView_OnSizeChanged",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "SetGridColumnCount(",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "NewSize.Width",
            codeBehind,
            StringComparison.Ordinal);
    }

    private static XElement FindByName(
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

    private static string? AttributeValue(
        XElement element,
        string localName)
    {
        return element
            .Attributes()
            .SingleOrDefault(
                attribute =>
                    attribute.Name.LocalName ==
                    localName)
            ?.Value;
    }

    private static XDocument LoadLibraryView()
    {
        return XDocument.Load(
            FindUiFile(
                Path.Combine(
                    "Library",
                    "LibraryView.xaml")));
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
