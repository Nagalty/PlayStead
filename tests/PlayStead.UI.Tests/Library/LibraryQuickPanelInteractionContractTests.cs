using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryQuickPanelInteractionContractTests
{
    [Fact]
    public void GameCard_exposes_selection_request_on_pointer_click()
    {
        var xaml =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Controls",
                        "GameCard.xaml")));

        var root =
            xaml.Root
            ?? throw new Xunit.Sdk.XunitException(
                "GameCard XAML root is missing.");

        Assert.Equal(
            "GameCard_OnMouseLeftButtonUp",
            root.Attribute(
                "MouseLeftButtonUp")?.Value);

        var codeBehind =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Controls",
                        "GameCard.xaml.cs")));

        Assert.Contains(
            "SelectionRequested",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "SelectionRequested?.Invoke",
            codeBehind,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Library_grid_hooks_card_selection_and_declares_quick_panel()
    {
        var xaml =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        var gameCard =
            xaml
                .Descendants()
                .Single(
                    element =>
                        element.Name.LocalName ==
                        "GameCard");

        Assert.Equal(
            "GameCard_OnSelectionRequested",
            gameCard.Attribute(
                "SelectionRequested")?.Value);

        var quickPanel =
            FindRequiredByName(
                xaml,
                "GameQuickPanel");

        Assert.Equal(
            "{Binding HasSelectedItem, Converter={StaticResource BooleanToVisibilityConverter}}",
            quickPanel.Attribute(
                "Visibility")?.Value);

        Assert.Equal(
            "{Binding SelectedItem}",
            quickPanel.Attribute(
                "Content")?.Value);

        Assert.NotNull(
            FindRequiredByName(
                xaml,
                "CloseQuickPanelButton"));

        Assert.NotNull(
            FindRequiredByName(
                xaml,
                "OpenGameDetailButton"));
    }

    [Fact]
    public void Library_view_selection_and_close_handlers_delegate_to_view_model()
    {
        var codeBehind =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml.cs")));

        Assert.Contains(
            "GameCard_OnSelectionRequested",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "viewModel.SelectGame(",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "CloseQuickPanelButton_OnClick",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "viewModel.ClearSelection()",
            codeBehind,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Library_view_exposes_detail_request_for_selected_game()
    {
        var codeBehind =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml.cs")));

        Assert.Contains(
            "GameDetailsRequested",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "OpenGameDetailButton_OnClick",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "GameDetailsRequested?.Invoke",
            codeBehind,
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
