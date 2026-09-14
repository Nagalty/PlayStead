using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Xml.Linq;
using PlayStead.UI.Home;

namespace PlayStead.UI.Tests.Home;

public sealed class HomeViewStructureTests
{
    [Fact]
    public void HomeView_is_a_UserControl_constructed_with_HomeViewModel()
    {
        var assembly =
            typeof(HomeViewModel)
                .Assembly;

        var type =
            assembly.GetType(
                "PlayStead.UI.Home.HomeView");

        Assert.NotNull(
            type);

        Assert.True(
            typeof(UserControl)
                .IsAssignableFrom(
                    type));

        var constructor =
            type.GetConstructor(
                BindingFlags.Public |
                BindingFlags.Instance,
                binder: null,
                [typeof(HomeViewModel)],
                modifiers: null);

        Assert.NotNull(
            constructor);
    }

    [Fact]
    public void HomeView_declares_compact_hero_and_library_KPI()
    {
        var document =
            LoadHomeView();

        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        var hero =
            Assert.Single(
                document.Descendants(
                    presentation + "Border"),
                element =>
                    (string?)element.Attribute(
                        xaml + "Name")
                    == "HomeHero");

        Assert.NotNull(
            hero);

        var textValues =
            document
                .Descendants(
                    presentation + "TextBlock")
                .Select(
                    element =>
                        (string?)element.Attribute(
                            "Text"))
                .Where(
                    value =>
                        value is not null)
                .Cast<string>()
                .ToArray();

        Assert.Contains(
            "PlayStead",
            textValues);

        Assert.Contains(
            "Accueil",
            textValues);

        var kpi =
            Assert.Single(
                document.Descendants(),
                element =>
                    element.Name.LocalName
                    == "KpiCard");

        var label =
            (string?)kpi.Attribute(
                "Label");

        var value =
            (string?)kpi.Attribute(
                "Value");

        Assert.NotNull(
            label);

        Assert.Contains(
            "jeu",
            label!,
            StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(
            value);

        Assert.Contains(
            "Binding LibraryGameCount",
            value!,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HomeView_declares_active_session_and_recent_activity_sections()
    {
        var document =
            LoadHomeView();

        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        var active =
            Assert.Single(
                document.Descendants(
                    presentation + "ItemsControl"),
                element =>
                    (string?)element.Attribute(
                        xaml + "Name")
                    == "ActiveSessionsList");

        Assert.Equal(
            "{Binding ActiveSessions}",
            (string?)active.Attribute(
                "ItemsSource"));

        var recent =
            Assert.Single(
                document.Descendants(
                    presentation + "ItemsControl"),
                element =>
                    (string?)element.Attribute(
                        xaml + "Name")
                    == "RecentActivityList");

        Assert.Equal(
            "{Binding RecentSessions}",
            (string?)recent.Attribute(
                "ItemsSource"));

        var headings =
            document
                .Descendants(
                    presentation + "TextBlock")
                .Select(
                    element =>
                        (string?)element.Attribute(
                            "Text"))
                .Where(
                    value =>
                        value is not null)
                .Cast<string>()
                .ToArray();

        Assert.Contains(
            headings,
            value =>
                value.Contains(
                    "session",
                    StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            headings,
            value =>
                value.Contains(
                    "activité récente",
                    StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HomeView_declares_contextual_navigation_actions()
    {
        var document =
            LoadHomeView();

        XNamespace presentation =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        XNamespace xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        var library =
            Assert.Single(
                document.Descendants(
                    presentation + "Button"),
                element =>
                    (string?)element.Attribute(
                        xaml + "Name")
                    == "OpenLibraryButton");

        Assert.Equal(
            "{Binding NavigateLibraryCommand}",
            (string?)library.Attribute(
                "Command"));

        var sessions =
            Assert.Single(
                document.Descendants(
                    presentation + "Button"),
                element =>
                    (string?)element.Attribute(
                        xaml + "Name")
                    == "OpenSessionsButton");

        Assert.Equal(
            "{Binding NavigateSessionsCommand}",
            (string?)sessions.Attribute(
                "Command"));
    }

    [Fact]
    public void HomeView_uses_existing_Dark_Copper_resources_and_no_analytics_chart_surface()
    {
        var path =
            FindUiFile(
                "Home/HomeView.xaml");

        var source =
            File.ReadAllText(
                path);

        Assert.Contains(
            "PlayStead.Brush.Background",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "PlayStead.Brush.Surface",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "PlayStead.Brush.Copper",
            source,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Chart",
            source,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "Graph",
            source,
            StringComparison.OrdinalIgnoreCase);
    }

    private static XDocument LoadHomeView()
    {
        return XDocument.Load(
            FindUiFile(
                "Home/HomeView.xaml"));
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
