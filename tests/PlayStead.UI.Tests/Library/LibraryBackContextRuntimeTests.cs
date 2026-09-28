using System.Xml.Linq;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryBackContextRuntimeTests
{
    [Fact]
    public void Back_from_game_detail_preserves_library_context()
    {
        var viewModel =
            new LibraryViewModel(
                new EmptyLibraryStore());

        var selectedGameId =
            GameId.New();

        viewModel.SetViewMode(
            LibraryViewMode.List);

        viewModel.SetSortKey(
            "Provider");

        viewModel.SetFilterKey(
            "Steam");

        viewModel.SetSelectedGame(
            selectedGameId);

        viewModel.SetVerticalOffset(
            318.5d);

        var expected =
            viewModel.CaptureUiState();

        var navigation =
            new NavigationService();

        navigation.Navigate(
            new NavigationRequest(
                AppRoute.Library));

        navigation.Navigate(
            new NavigationRequest(
                AppRoute.GameDetail,
                selectedGameId));

        Assert.Equal(
            AppRoute.GameDetail,
            navigation.CurrentRoute);

        Assert.True(
            navigation.GoBack());

        Assert.Equal(
            AppRoute.Library,
            navigation.CurrentRoute);

        Assert.Equal(
            expected,
            viewModel.CaptureUiState());
    }

    [Fact]
    public void View_state_adapter_captures_and_returns_vertical_offset()
    {
        var viewModel =
            new LibraryViewModel(
                new EmptyLibraryStore());

        var adapter =
            new LibraryViewStateAdapter();

        adapter.CaptureVerticalOffset(
            viewModel,
            247.75d);

        Assert.Equal(
            247.75d,
            viewModel.VerticalOffset);

        Assert.Equal(
            247.75d,
            adapter.GetRestoreVerticalOffset(
                viewModel));
    }

    [Fact]
    public void Grid_and_list_hosts_provide_internal_scroll_surfaces()
    {
        var document =
            XDocument.Load(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml")));

        var gridHost =
            FindRequiredByName(
                document,
                "GameGridRows");

        var listHost =
            FindRequiredByName(
                document,
                "GameList");

        Assert.Contains(
            gridHost.Descendants(),
            element =>
                element.Name.LocalName ==
                "ScrollViewer");

        Assert.Contains(
            listHost.Descendants(),
            element =>
                element.Name.LocalName ==
                "ScrollViewer");
    }

    [Fact]
    public void Library_view_wires_scroll_capture_and_restore_through_adapter()
    {
        var codeBehind =
            File.ReadAllText(
                FindUiFile(
                    Path.Combine(
                        "Library",
                        "LibraryView.xaml.cs")));

        Assert.Contains(
            "LibraryViewStateAdapter",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "CaptureVerticalOffset(",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "GetRestoreVerticalOffset(",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "ScrollToVerticalOffset(",
            codeBehind,
            StringComparison.Ordinal);

        Assert.Contains(
            "RestoreSavedScrollPosition",
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

    private sealed class EmptyLibraryStore :
        ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                new LibrarySnapshot(
                    Array.Empty<LogicalGame>(),
                    Array.Empty<GameInstallation>()));
        }
    }
}
