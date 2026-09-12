using System.ComponentModel;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryViewModelObservableTests
{
    [Fact]
    public async Task RefreshAsync_notifies_Items_and_HasItems_when_snapshot_changes()
    {
        var now = new DateTimeOffset(
            2026, 9, 12, 10, 30, 0, TimeSpan.Zero);

        var gameId = GameId.New();

        var empty = new LibrarySnapshot(
            Array.Empty<LogicalGame>(),
            Array.Empty<GameInstallation>());

        var populated = new LibrarySnapshot(
            [
                new LogicalGame(
                    gameId,
                    "Arma Reforger",
                    IsHidden: false,
                    CreatedAtUtc: now,
                    UpdatedAtUtc: now)
            ],
            [
                new GameInstallation(
                    InstallationId.New(),
                    gameId,
                    ProviderKind.Steam,
                    "1874880",
                    @"G:\SteamLibrary\steamapps\common\Arma Reforger",
                    42_000_000_000,
                    IsPreferred: true,
                    IsPresent: true,
                    LastSeenUtc: now)
            ]);

        var store = new SequenceLibraryStore(empty, populated);
        var sut = new LibraryViewModel(store);

        var observable = Assert.IsAssignableFrom<INotifyPropertyChanged>(sut);

        var changed = new List<string>();

        observable.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changed.Add(args.PropertyName);
            }
        };

        await sut.RefreshAsync(CancellationToken.None);

        Assert.False(sut.HasItems);
        Assert.Empty(sut.Items);

        changed.Clear();

        await sut.RefreshAsync(CancellationToken.None);

        Assert.True(sut.HasItems);
        Assert.Single(sut.Items);
        Assert.Contains(nameof(LibraryViewModel.Items), changed);
        Assert.Contains(nameof(LibraryViewModel.HasItems), changed);
    }

    private sealed class SequenceLibraryStore(
        params LibrarySnapshot[] snapshots) : ILibraryStore
    {
        private readonly Queue<LibrarySnapshot> _snapshots =
            new(snapshots);

        public Task ApplySourceScanAsync(
            SourceScanResult result,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LibrarySnapshot> LoadSnapshotAsync(
            CancellationToken cancellationToken)
        {
            if (_snapshots.Count == 0)
            {
                throw new InvalidOperationException(
                    "No more snapshots configured.");
            }

            return Task.FromResult(_snapshots.Dequeue());
        }
    }
}
