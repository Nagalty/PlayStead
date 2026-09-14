using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryVirtualizationContractTests
{
    private const string GridHostName =
        "GameGridRows";

    private const string ListHostName =
        "GameList";

    private const string GridRowsBinding =
        "{Binding GridRows}";

    private const string ItemsBinding =
        "{Binding Items}";

    [Fact]
    public void Library_declares_separate_grid_and_list_hosts()
    {
        var document =
            LoadLibraryView();

        var gridHost =
            FindRequiredNamedHost(
                document,
                GridHostName);

        var listHost =
            FindRequiredNamedHost(
                document,
                ListHostName);

        Assert.NotSame(
            gridHost,
            listHost);

        Assert.Equal(
            GridRowsBinding,
            AttributeValue(
                gridHost,
                "ItemsSource"));

        Assert.Equal(
            ItemsBinding,
            AttributeValue(
                listHost,
                "ItemsSource"));
    }

    [Fact]
    public void Grid_host_uses_recycling_VirtualizingStackPanel()
    {
        var host =
            FindRequiredNamedHost(
                LoadLibraryView(),
                GridHostName);

        AssertVirtualized(
            host);
    }

    [Fact]
    public void List_host_uses_recycling_VirtualizingStackPanel()
    {
        var host =
            FindRequiredNamedHost(
                LoadLibraryView(),
                ListHostName);

        AssertVirtualized(
            host);
    }

    [Fact]
    public void Library_does_not_wrap_virtualized_hosts_in_an_outer_ScrollViewer()
    {
        var document =
            LoadLibraryView();

        var gridHost =
            FindRequiredNamedHost(
                document,
                GridHostName);

        var listHost =
            FindRequiredNamedHost(
                document,
                ListHostName);

        Assert.DoesNotContain(
            gridHost.Ancestors(),
            element =>
                element.Name.LocalName ==
                "ScrollViewer");

        Assert.DoesNotContain(
            listHost.Ancestors(),
            element =>
                element.Name.LocalName ==
                "ScrollViewer");
    }

    [Fact]
    public void Library_grid_does_not_use_a_WrapPanel()
    {
        var host =
            FindRequiredNamedHost(
                LoadLibraryView(),
                GridHostName);

        Assert.DoesNotContain(
            host.Descendants(),
            element =>
                element.Name.LocalName ==
                "WrapPanel");
    }

    private static void AssertVirtualized(
        XElement host)
    {
        Assert.Equal(
            "True",
            AttributeValue(
                host,
                "VirtualizingPanel.IsVirtualizing"));

        Assert.Equal(
            "Recycling",
            AttributeValue(
                host,
                "VirtualizingPanel.VirtualizationMode"));

        var itemsPanel =
            host
                .Elements()
                .SingleOrDefault(
                    element =>
                        element.Name.LocalName
                            .EndsWith(
                                ".ItemsPanel",
                                StringComparison.Ordinal));

        Assert.NotNull(
            itemsPanel);

        Assert.Contains(
            itemsPanel.Descendants(),
            element =>
                element.Name.LocalName ==
                "VirtualizingStackPanel");
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

    private static XElement FindRequiredNamedHost(
        XDocument document,
        string name)
    {
        return document
                   .Descendants()
                   .Where(
                       element =>
                           element.Name.LocalName is
                               "ItemsControl" or
                               "ListBox")
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
                   $"Library host with x:Name '{name}' was not found.");
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
