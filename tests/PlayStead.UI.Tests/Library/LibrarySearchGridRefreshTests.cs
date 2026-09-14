using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibrarySearchGridRefreshTests
{
    [Fact]
    public async Task Changing_search_query_notifies_both_list_and_grid_projections()
    {
        var viewModel =
            new LibraryViewModel(
                new SearchLibraryStore());

        await viewModel.RefreshAsync(
            CancellationToken.None);

        var notifications =
            new List<string?>();

        viewModel.PropertyChanged +=
            (_, e) =>
                notifications.Add(
                    e.PropertyName);

        viewModel.SetSearchQuery(
            "A");

        Assert.Contains(
            nameof(LibraryViewModel.VisibleItems),
            notifications);

        Assert.Contains(
            nameof(LibraryViewModel.GridRows),
            notifications);
    }

    private sealed class SearchLibraryStore :
        ILibraryStore
    {
        private readonly GameId _armaId =
            GameId.New();

        private readonly GameId _helldiversId =
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
                    15,
                    45,
                    0,
                    TimeSpan.Zero);

            return Task.FromResult(
                new LibrarySnapshot(
                    Games:
                    [
                        new LogicalGame(
                            _armaId,
                            "Arma Reforger",
                            IsHidden: false,
                            CreatedAtUtc: now,
                            UpdatedAtUtc: now),

                        new LogicalGame(
                            _helldiversId,
                            "HELLDIVERS™ 2",
                            IsHidden: false,
                            CreatedAtUtc: now,
                            UpdatedAtUtc: now)
                    ],
                    Installations:
                    [
                        new GameInstallation(
                            InstallationId.New(),
                            _armaId,
                            ProviderKind.Steam,
                            "1874880",
                            @"G:\SteamLibrary\steamapps\common\Arma Reforger",
                            27_000_000_000,
                            IsPreferred: true,
                            IsPresent: true,
                            LastSeenUtc: now),

                        new GameInstallation(
                            InstallationId.New(),
                            _helldiversId,
                            ProviderKind.Steam,
                            "553850",
                            @"G:\SteamLibrary\steamapps\common\Helldivers 2",
                            24_000_000_000,
                            IsPreferred: true,
                            IsPresent: true,
                            LastSeenUtc: now)
                    ]));
        }
    }
}
