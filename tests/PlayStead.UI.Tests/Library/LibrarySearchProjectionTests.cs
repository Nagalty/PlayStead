using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibrarySearchProjectionTests
{
    [Fact]
    public async Task Search_query_filters_virtualized_grid_rows()
    {
        var viewModel =
            new LibraryViewModel(
                new SearchLibraryStore());

        await viewModel.RefreshAsync(
            CancellationToken.None);

        viewModel.SetGridColumnCount(
            2);

        viewModel.SetSearchQuery(
            "beta");

        var visibleGridItems =
            viewModel.GridRows
                .SelectMany(
                    row =>
                        row.Items)
                .ToArray();

        Assert.Single(
            visibleGridItems);

        Assert.Equal(
            "Beta",
            visibleGridItems[0].Title);
    }

    private sealed class SearchLibraryStore :
        ILibraryStore
    {
        private readonly GameId _alphaId =
            GameId.New();

        private readonly GameId _betaId =
            GameId.New();

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

            var now =
                new DateTimeOffset(
                    2026,
                    9,
                    14,
                    10,
                    0,
                    0,
                    TimeSpan.Zero);

            return Task.FromResult(
                new LibrarySnapshot(
                    Games:
                    [
                        new LogicalGame(
                            _alphaId,
                            "Alpha",
                            IsHidden: false,
                            CreatedAtUtc: now,
                            UpdatedAtUtc: now),

                        new LogicalGame(
                            _betaId,
                            "Beta",
                            IsHidden: false,
                            CreatedAtUtc: now,
                            UpdatedAtUtc: now)
                    ],
                    Installations:
                    [
                        new GameInstallation(
                            InstallationId.New(),
                            _alphaId,
                            ProviderKind.Steam,
                            "100",
                            @"D:\Games\Alpha",
                            10_000_000_000,
                            IsPreferred: true,
                            IsPresent: true,
                            LastSeenUtc: now),

                        new GameInstallation(
                            InstallationId.New(),
                            _betaId,
                            ProviderKind.Manual,
                            "beta",
                            @"D:\Games\Beta",
                            20_000_000_000,
                            IsPreferred: true,
                            IsPresent: true,
                            LastSeenUtc: now)
                    ]));
        }
    }
}
