using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryQuickPanelSelectionTests
{
    [Fact]
    public void Selecting_game_exposes_selected_item_for_quick_panel()
    {
        var viewModel =
            new LibraryViewModel(
                new EmptyLibraryStore());

        var item =
            new LibraryItemViewModel(
                GameId.New(),
                "Test Game",
                default,
                "Provider",
                @"C:\Games\TestGame",
                42_000_000_000);

        viewModel.SelectGame(
            item);

        Assert.Same(
            item,
            viewModel.SelectedItem);

        Assert.True(
            viewModel.HasSelectedItem);
    }

    [Fact]
    public void Clearing_selection_hides_quick_panel_state()
    {
        var viewModel =
            new LibraryViewModel(
                new EmptyLibraryStore());

        var item =
            new LibraryItemViewModel(
                GameId.New(),
                "Test Game",
                default,
                "Provider",
                @"C:\Games\TestGame",
                42_000_000_000);

        viewModel.SelectGame(
            item);

        viewModel.ClearSelection();

        Assert.Null(
            viewModel.SelectedItem);

        Assert.False(
            viewModel.HasSelectedItem);
    }

    private sealed class EmptyLibraryStore :
        ILibraryStore
    {
        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new LibrarySnapshot(
                    Array.Empty<LogicalGame>(),
                    Array.Empty<GameInstallation>()));
        }
    }
}
