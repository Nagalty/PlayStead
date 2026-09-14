using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryUiStateTests
{
    [Fact]
    public void New_library_state_defaults_to_Grid_with_no_selection_and_zero_scroll()
    {
        var sut =
            new LibraryViewModel(
                new EmptyLibraryStore());

        Assert.Equal(
            LibraryViewMode.Grid,
            sut.ViewMode);

        Assert.NotNull(
            sut.SortKey);

        Assert.Null(
            sut.FilterKey);

        Assert.Null(
            sut.SelectedGameId);

        Assert.Equal(
            0d,
            sut.VerticalOffset);
    }

    [Fact]
    public void View_mode_can_toggle_between_Grid_and_List()
    {
        var sut =
            new LibraryViewModel(
                new EmptyLibraryStore());

        sut.SetViewMode(
            LibraryViewMode.List);

        Assert.Equal(
            LibraryViewMode.List,
            sut.ViewMode);

        sut.SetViewMode(
            LibraryViewMode.Grid);

        Assert.Equal(
            LibraryViewMode.Grid,
            sut.ViewMode);
    }

    [Fact]
    public void CaptureUiState_keeps_mode_sort_filter_selection_and_scroll_offset()
    {
        var sut =
            new LibraryViewModel(
                new EmptyLibraryStore());

        var selectedGameId =
            new GameId(
                Guid.NewGuid());

        sut.SetViewMode(
            LibraryViewMode.List);

        sut.SetSelectedGame(
            selectedGameId);

        sut.SetVerticalOffset(
            347.5d);

        var state =
            sut.CaptureUiState();

        Assert.Equal(
            LibraryViewMode.List,
            state.ViewMode);

        Assert.Equal(
            sut.SortKey,
            state.SortKey);

        Assert.Equal(
            sut.FilterKey,
            state.FilterKey);

        Assert.Equal(
            selectedGameId,
            state.SelectedGameId);

        Assert.Equal(
            347.5d,
            state.VerticalOffset);
    }

    [Fact]
    public void RestoreUiState_restores_mode_sort_filter_selection_and_scroll_offset()
    {
        var sut =
            new LibraryViewModel(
                new EmptyLibraryStore());

        var selectedGameId =
            new GameId(
                Guid.NewGuid());

        var state =
            new LibraryUiState(
                LibraryViewMode.List,
                sut.SortKey,
                FilterKey: null,
                selectedGameId,
                VerticalOffset: 812.25d);

        sut.RestoreUiState(
            state);

        Assert.Equal(
            state.ViewMode,
            sut.ViewMode);

        Assert.Equal(
            state.SortKey,
            sut.SortKey);

        Assert.Equal(
            state.FilterKey,
            sut.FilterKey);

        Assert.Equal(
            state.SelectedGameId,
            sut.SelectedGameId);

        Assert.Equal(
            state.VerticalOffset,
            sut.VerticalOffset);
    }

    [Fact]
    public void RestoreUiState_rejects_null_state()
    {
        var sut =
            new LibraryViewModel(
                new EmptyLibraryStore());

        Assert.Throws<
            ArgumentNullException>(
            () =>
                sut.RestoreUiState(
                    null!));
    }

    private sealed class EmptyLibraryStore :
        ILibraryStore
    {
        private static readonly LibrarySnapshot EmptySnapshot =
            new(
                Array.Empty<LogicalGame>(),
                Array.Empty<GameInstallation>());

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
                EmptySnapshot);
        }
    }
}
