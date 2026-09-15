using System.Xml.Linq;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryMediaVirtualizationContractTests
{
    [Fact]
    public void RefreshAsync_does_not_start_remote_media_resolution()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryViewModel.cs")));

        var refreshStart =
            source.IndexOf(
                "public async Task RefreshAsync(",
                StringComparison.Ordinal);

        var nextMethod =
            source.IndexOf(
                "public Task VerifySteamAsync(",
                refreshStart,
                StringComparison.Ordinal);

        Assert.True(
            refreshStart >= 0 &&
            nextMethod > refreshStart,
            "Could not isolate LibraryViewModel.RefreshAsync.");

        var refreshBody =
            source[refreshStart..nextMethod];

        Assert.DoesNotContain(
            "ResolveAndCacheAsync",
            refreshBody,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "EnsureCoverAsync",
            refreshBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GameCard_requests_media_from_the_loaded_lifecycle_only()
    {
        var source =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Controls",
                        "GameCard.xaml.cs")));

        Assert.Contains(
            "Loaded +=",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "MediaRequested?.Invoke",
            source,
            StringComparison.Ordinal);

        var constructorStart =
            source.IndexOf(
                "public GameCard()",
                StringComparison.Ordinal);

        var firstPrivateMember =
            source.IndexOf(
                "\n    private ",
                constructorStart,
                StringComparison.Ordinal);

        Assert.True(
            constructorStart >= 0 &&
            firstPrivateMember > constructorStart,
            "Could not isolate GameCard constructor.");

        var constructorBody =
            source[constructorStart..firstPrivateMember];

        Assert.DoesNotContain(
            "MediaRequested?.Invoke",
            constructorBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Grid_and_list_keep_recycling_virtualization()
    {
        var document =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        var virtualizingPanels =
            document
                .Descendants()
                .Where(
                    element =>
                        element.Name.LocalName ==
                        "VirtualizingStackPanel")
                .ToArray();

        Assert.True(
            virtualizingPanels.Length >= 2,
            $"Expected Grid and List virtualization panels, found {virtualizingPanels.Length}.");

        var allAttributes =
            document
                .Root!
                .DescendantsAndSelf()
                .SelectMany(
                    element =>
                        element.Attributes())
                .ToArray();

        Assert.True(
            allAttributes.Count(
                attribute =>
                    attribute.Name.LocalName.EndsWith(
                        "IsVirtualizing",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        attribute.Value,
                        "True",
                        StringComparison.OrdinalIgnoreCase)) >= 2,
            "Grid and List must explicitly enable item virtualization.");

        Assert.True(
            allAttributes.Count(
                attribute =>
                    attribute.Name.LocalName.EndsWith(
                        "VirtualizationMode",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        attribute.Value,
                        "Recycling",
                        StringComparison.OrdinalIgnoreCase)) >= 2,
            "Grid and List must use recycling virtualization.");
    }

    [Fact]
    public void Grid_and_list_are_not_wrapped_by_an_outer_ScrollViewer()
    {
        var document =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        foreach (var hostName in new[]
                 {
                     "GameGridRows",
                     "GameList"
                 })
        {
            var host =
                Assert.Single(
                    document.Descendants(),
                    element =>
                        element
                            .Attributes()
                            .Any(
                                attribute =>
                                    attribute.Name.LocalName ==
                                        "Name" &&
                                    attribute.Value ==
                                        hostName));

            Assert.DoesNotContain(
                host.Ancestors(),
                ancestor =>
                    ancestor.Name.LocalName ==
                    "ScrollViewer");
        }
    }

    private static string FindUiFile(
        string relativePath)
    {
        for (var directory =
                 new DirectoryInfo(
                     AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path =
                Path.Combine(
                    directory.FullName,
                    "src",
                    "PlayStead.UI",
                    relativePath);

            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            $"Required UI file missing: {relativePath}");
    }
}
