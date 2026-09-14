using System.ComponentModel;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryResponsiveGridTests
{
    [Fact]
    public void Grid_projection_defaults_to_one_column_and_empty_rows()
    {
        var sut =
            new LibraryViewModel(
                new EmptyLibraryStore());

        Assert.Equal(
            1,
            sut.GridColumnCount);

        Assert.Empty(
            sut.GridRows);
    }

    [Fact]
    public void SetGridColumnCount_updates_count_and_notifies_grid_projection()
    {
        var sut =
            new LibraryViewModel(
                new EmptyLibraryStore());

        var changed =
            new List<string?>();

        sut.PropertyChanged +=
            (_, e) =>
                changed.Add(
                    e.PropertyName);

        sut.SetGridColumnCount(
            4);

        Assert.Equal(
            4,
            sut.GridColumnCount);

        Assert.Contains(
            nameof(LibraryViewModel.GridColumnCount),
            changed);

        Assert.Contains(
            nameof(LibraryViewModel.GridRows),
            changed);
    }

    [Fact]
    public void SetGridColumnCount_rejects_non_positive_values()
    {
        var sut =
            new LibraryViewModel(
                new EmptyLibraryStore());

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                sut.SetGridColumnCount(
                    0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                sut.SetGridColumnCount(
                    -1));
    }

    [Fact]
    public void View_mode_exposes_grid_and_list_visibility_flags()
    {
        var sut =
            new LibraryViewModel(
                new EmptyLibraryStore());

        Assert.True(
            sut.IsGridMode);

        Assert.False(
            sut.IsListMode);

        sut.SetViewMode(
            LibraryViewMode.List);

        Assert.False(
            sut.IsGridMode);

        Assert.True(
            sut.IsListMode);

        sut.SetViewMode(
            LibraryViewMode.Grid);

        Assert.True(
            sut.IsGridMode);

        Assert.False(
            sut.IsListMode);
    }

    [Fact]
    public void Changing_view_mode_notifies_visibility_flags()
    {
        var sut =
            new LibraryViewModel(
                new EmptyLibraryStore());

        var changed =
            new List<string?>();

        sut.PropertyChanged +=
            (_, e) =>
                changed.Add(
                    e.PropertyName);

        sut.SetViewMode(
            LibraryViewMode.List);

        Assert.Contains(
            nameof(LibraryViewModel.ViewMode),
            changed);

        Assert.Contains(
            nameof(LibraryViewModel.IsGridMode),
            changed);

        Assert.Contains(
            nameof(LibraryViewModel.IsListMode),
            changed);
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
