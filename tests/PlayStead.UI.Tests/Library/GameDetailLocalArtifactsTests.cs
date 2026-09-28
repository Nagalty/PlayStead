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

    [Fact]
    public async Task Baseline_status_is_projected_without_exposing_hash()
    {
        var game = CreateGame();
        var path = Path.Combine(Path.GetTempPath(), $"playstead-ui-baseline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        try
        {
            var artifact = new GameLocalArtifact(game.GameId, GameLocalArtifactKind.Configuration, path, GameLocalArtifactSource.KnownConvention, GameLocalArtifactStatus.KnownAndExists, "rule-a");
            var baseline = new LocalArtifactBaseline(game.GameId, artifact.Kind, "rule-a", "SHA256", "ABC", 0, 0, DateTimeOffset.UtcNow);
            var viewModel = new GameDetailViewModel(
                game, launch: null, activity: null, heroPath: null,
                localArtifactDiscoveryService: new FakeDiscoveryService([artifact]),
                artifactFingerprintService: new FakeFingerprintService(new ArtifactFingerprintResult(new ArtifactFingerprint("SHA256", "ABC", 0, 0, DateTimeOffset.UtcNow), null)),
                artifactBaselineStore: new FakeBaselineStore(baseline));

            await viewModel.LoadAsync(CancellationToken.None);

            var projected = Assert.Single(viewModel.LocalArtifacts);
            Assert.Equal(LocalArtifactBaselineStatus.Unchanged, projected.BaselineStatus);
            Assert.Equal("Conforme à la référence", projected.BaselineStatusLabel);
        }
        finally { Directory.Delete(path, recursive: true); }
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

    private sealed class FakeFingerprintService(ArtifactFingerprintResult result) : IArtifactFingerprintService
    {
        public Task<ArtifactFingerprintResult> ComputeAsync(GameLocalArtifact artifact, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FakeBaselineStore(LocalArtifactBaseline? baseline) : ILocalArtifactBaselineStore
    {
        public Task<LocalArtifactBaseline?> GetAsync(GameId gameId, GameLocalArtifactKind kind, string artifactIdentity, CancellationToken cancellationToken) => Task.FromResult(baseline);
        public Task UpsertAsync(LocalArtifactBaseline baseline, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
