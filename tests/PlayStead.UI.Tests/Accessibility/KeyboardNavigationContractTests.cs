using System.Xml.Linq;

namespace PlayStead.UI.Tests.Accessibility;

public sealed class KeyboardNavigationContractTests
{
    [Fact]
    public void Primary_navigation_and_search_remain_keyboard_reachable()
    {
        var shell =
            XDocument.Load(
                FindUiFile(
                    "MainWindow.xaml"));

        foreach (var name in new[]
                 {
                     "HomeNavButton",
                     "LibraryNavButton",
                     "AttentionNavButton",
                     "SettingsNavButton"
                 })
        {
            var button =
                FindRequiredByName(
                    shell,
                    name);

            Assert.NotEqual(
                "False",
                button.Attribute(
                    "Focusable")?.Value);

            Assert.NotEqual(
                "False",
                button.Attribute(
                    "IsTabStop")?.Value);
        }

        var library =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        var searchBox =
            FindRequiredByName(
                library,
                "LibrarySearchBox");

        Assert.NotEqual(
            "False",
            searchBox.Attribute(
                "Focusable")?.Value);

        Assert.NotEqual(
            "False",
            searchBox.Attribute(
                "IsTabStop")?.Value);
    }

    [Fact]
    public void Icon_only_actions_expose_tooltips_and_accessible_names()
    {
        var library =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        AssertIconOnlyActionIsAccessible(
            FindRequiredByName(
                library,
                "CloseQuickPanelButton"));

        AssertIconOnlyActionIsAccessible(
            FindRequiredByName(
                library,
                "ClearSearchButton"));

        var splitButton =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Controls",
                        "PlaySplitButton.xaml")));

        var optionButton =
            splitButton
                .Descendants()
                .Single(
                    element =>
                        element.Name.LocalName == "Button" &&
                        element.Attribute("Content")?.Value == "▾");

        AssertIconOnlyActionIsAccessible(
            optionButton);
    }

    [Fact]
    public void Status_semantics_are_expressed_with_text_not_copper_alone()
    {
        var librarySource =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        Assert.Contains(
            "SessionStatusLabel",
            librarySource,
            StringComparison.Ordinal);

        Assert.Contains(
            "SteamStatusLabel",
            librarySource,
            StringComparison.Ordinal);

        var sessionDetailSource =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Sessions",
                        "SessionDetailView.xaml")));

        Assert.Contains(
            "Corrigée",
            sessionDetailSource,
            StringComparison.Ordinal);

        Assert.Contains(
            "Récupérée",
            sessionDetailSource,
            StringComparison.Ordinal);
    }

    private static void AssertIconOnlyActionIsAccessible(
        XElement element)
    {
        var toolTip =
            element.Attribute(
                "ToolTip")?.Value;

        Assert.False(
            string.IsNullOrWhiteSpace(
                toolTip),
            "Icon-only actions require a descriptive ToolTip.");

        var automationName =
            element.Attributes()
                .SingleOrDefault(
                    attribute =>
                        attribute.Name.LocalName ==
                        "Name" &&
                        attribute.Name.NamespaceName.Contains(
                            "automation",
                            StringComparison.OrdinalIgnoreCase))
                ?.Value;

        if (string.IsNullOrWhiteSpace(
                automationName))
        {
            automationName =
                element.Attributes()
                    .SingleOrDefault(
                        attribute =>
                            attribute.Name.LocalName ==
                            "AutomationProperties.Name")
                    ?.Value;
        }

        Assert.False(
            string.IsNullOrWhiteSpace(
                automationName),
            "Icon-only actions require AutomationProperties.Name in addition to their tooltip.");
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
