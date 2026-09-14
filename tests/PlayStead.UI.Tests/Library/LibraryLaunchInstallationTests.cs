using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.UI.Library;

namespace PlayStead.UI.Tests.Library;

public sealed class LibraryLaunchInstallationTests
{
    [Fact]
    public async Task Default_launch_installation_uses_preferred_present_installation_from_snapshot()
    {
        var gameId =
            GameId.New();

        var other =
            CreateInstallation(
                gameId,
                ProviderKind.Steam,
                "111",
                isPreferred: false,
                isPresent: true);

        var preferred =
            CreateInstallation(
                gameId,
                ProviderKind.Steam,
                "222",
                isPreferred: true,
                isPresent: true);

        var absent =
            CreateInstallation(
                gameId,
                ProviderKind.Steam,
                "333",
                isPreferred: true,
                isPresent: false);

        var viewModel =
            new LibraryViewModel(
                new LaunchLibraryStore(
                    gameId,
                    [other, absent, preferred]));

        await viewModel.RefreshAsync(
            CancellationToken.None);

        var selected =
            viewModel.GetDefaultLaunchInstallation(
                gameId);

        Assert.Same(
            preferred,
            selected);
    }

    [Fact]
    public async Task Default_launch_installation_returns_null_for_unknown_game()
    {
        var gameId =
            GameId.New();

        var viewModel =
            new LibraryViewModel(
                new LaunchLibraryStore(
                    gameId,
                    [
                        CreateInstallation(
                            gameId,
                            ProviderKind.Steam,
                            "111",
                            isPreferred: true,
                            isPresent: true)
                    ]));

        await viewModel.RefreshAsync(
            CancellationToken.None);

        Assert.Null(
            viewModel.GetDefaultLaunchInstallation(
                GameId.New()));
    }

    private static GameInstallation CreateInstallation(
        GameId gameId,
        ProviderKind provider,
        string externalId,
        bool isPreferred,
        bool isPresent)
    {
        return new GameInstallation(
            InstallationId.New(),
            gameId,
            provider,
            externalId,
            $@"D:\Games\{externalId}",
            1_000_000_000,
            isPreferred,
            isPresent,
            new DateTimeOffset(
                2026,
                9,
                14,
                13,
                30,
                0,
                TimeSpan.Zero));
    }

    private sealed class LaunchLibraryStore :
        ILibraryStore
    {
        private readonly GameId _gameId;
        private readonly IReadOnlyList<GameInstallation>
            _installations;

        public LaunchLibraryStore(
            GameId gameId,
            IReadOnlyList<GameInstallation> installations)
        {
            _gameId =
                gameId;

            _installations =
                installations;
        }

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
                    13,
                    30,
                    0,
                    TimeSpan.Zero);

            return Task.FromResult(
                new LibrarySnapshot(
                    Games:
                    [
                        new LogicalGame(
                            _gameId,
                            "Test Game",
                            IsHidden: false,
                            CreatedAtUtc: now,
                            UpdatedAtUtc: now)
                    ],
                    Installations:
                        _installations));
        }
    }
}
