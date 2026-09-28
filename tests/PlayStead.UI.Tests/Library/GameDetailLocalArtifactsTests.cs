using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Core.Steam;
using PlayStead.UI.Library;
using PlayStead.UI.Navigation;

namespace PlayStead.UI.Tests.Library;

public sealed class GameDetailLocalArtifactsTests
{
    [Fact]
    public async Task Known_artifacts_are_projected_and_missing_status_is_preserved()
    {
        var game = CreateGame();
        var artifacts = new[]
        {
            new GameLocalArtifact(game.GameId, GameLocalArtifactKind.Configuration, @"C:\Config", GameLocalArtifactSource.ExplicitRule, GameLocalArtifactStatus.KnownAndExists),
            new GameLocalArtifact(game.GameId, GameLocalArtifactKind.SaveData, @"C:\Saves", GameLocalArtifactSource.ExplicitRule, GameLocalArtifactStatus.KnownButMissing),
            new GameLocalArtifact(game.GameId, GameLocalArtifactKind.Log, @"C:\Logs", GameLocalArtifactSource.ExplicitRule, GameLocalArtifactStatus.KnownAndExists)
        };
        var viewModel = CreateViewModel(game, artifacts);

        await viewModel.LoadAsync(CancellationToken.None);

        Assert.True(viewModel.HasLocalArtifacts);
        Assert.Equal(3, viewModel.LocalArtifacts.Count);
        Assert.Equal(GameLocalArtifactStatus.KnownButMissing, viewModel.LocalArtifacts[1].Status);
    }

    [Fact]
    public async Task No_known_artifacts_hide_the_section_projection()
    {
        var game = CreateGame();
        var viewModel = CreateViewModel(game, []);

        await viewModel.LoadAsync(CancellationToken.None);

        Assert.False(viewModel.HasLocalArtifacts);
        Assert.Empty(viewModel.LocalArtifacts);
    }

    private static GameDetailViewModel CreateViewModel(LibraryItemViewModel game, IReadOnlyList<GameLocalArtifact> artifacts) =>
        new(game, launch: null, activity: null, heroPath: null,
            localArtifactDiscoveryService: new FakeDiscoveryService(artifacts));

    private static LibraryItemViewModel CreateGame() =>
        new(GameId.New(), "Game", ProviderKind.Steam, "Steam", @"C:\Game", null);

    private sealed class FakeDiscoveryService(IReadOnlyList<GameLocalArtifact> artifacts) : IGameLocalArtifactDiscoveryService
    {
        public Task<IReadOnlyList<GameLocalArtifact>> DiscoverAsync(GameId gameId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameLocalArtifact>>(artifacts);
    }
}
