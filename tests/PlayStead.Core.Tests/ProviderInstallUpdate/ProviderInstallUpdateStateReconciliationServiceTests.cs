using PlayStead.Core.Library;
using PlayStead.Core.GameBuildHistory;
using PlayStead.Core.ProviderInstallUpdate;

namespace PlayStead.Core.Tests.ProviderInstallUpdate;

public sealed class ProviderInstallUpdateStateReconciliationServiceTests
{
    [Fact]
    public async Task Same_semantic_state_with_new_observation_time_does_not_raise_changed()
    {
        var source = new FakeSource();
        var service = new ProviderInstallUpdateStateReconciliationService([source]);
        var installation = Installation();
        var first = State(installation, new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero));
        var second = State(installation, new(2026, 9, 24, 12, 1, 0, TimeSpan.Zero));
        var raised = 0;
        service.Changed += (_, _) => raised++;

        source.Values = [first];
        await service.RefreshAsync([installation], CancellationToken.None);
        source.Values = [second];
        await service.RefreshAsync([installation], CancellationToken.None);

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Semantic_status_change_raises_changed()
    {
        var source = new FakeSource();
        var service = new ProviderInstallUpdateStateReconciliationService([source]);
        var installation = Installation();
        var raised = 0;
        service.Changed += (_, _) => raised++;

        source.Values = [State(installation, DateTimeOffset.UtcNow, ProviderInstallUpdateStatus.UpToDate)];
        await service.RefreshAsync([installation], CancellationToken.None);
        source.Values = [State(installation, DateTimeOffset.UtcNow, ProviderInstallUpdateStatus.UpdateAvailable)];
        await service.RefreshAsync([installation], CancellationToken.None);

        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task Installed_build_is_fed_to_history_during_refresh()
    {
        var source = new FakeSource();
        var history = new FakeHistoryStore();
        var service = new ProviderInstallUpdateStateReconciliationService(
            [source],
            new GameBuildHistoryService(history));
        var installation = Installation();
        source.Values = [State(installation, DateTimeOffset.UtcNow)];

        await service.RefreshAsync([installation], CancellationToken.None);
        await service.RefreshAsync([installation], CancellationToken.None);

        var observation = Assert.Single(history.Observations);
        Assert.Equal(installation.GameId, observation.GameId);
        Assert.Equal("100", observation.BuildId);
        Assert.Equal("553850", observation.ProviderGameId);
    }

    private static GameInstallation Installation() =>
        new(new InstallationId(Guid.NewGuid()), new GameId(Guid.NewGuid()), ProviderKind.Steam,
            "553850", @"G:\SteamLibrary\steamapps\common\Helldivers 2", null, true, true, DateTimeOffset.UtcNow);

    private static ProviderInstallUpdateState State(GameInstallation installation, DateTimeOffset observed,
        ProviderInstallUpdateStatus status = ProviderInstallUpdateStatus.UpToDate) =>
        new(installation.GameId, installation.Provider, installation.ExternalId, "100", status == ProviderInstallUpdateStatus.UpToDate ? "100" : "101", status,
            null, null, null, null, null, null, observed);

    private sealed class FakeSource : IProviderInstallUpdateStateSource
    {
        public ProviderKind Provider => ProviderKind.Steam;
        public IReadOnlyList<ProviderInstallUpdateState> Values { get; set; } = [];
        public Task<IReadOnlyList<ProviderInstallUpdateState>> GetAsync(IReadOnlyCollection<GameInstallation> installations, CancellationToken cancellationToken) =>
            Task.FromResult(Values);
    }

    private sealed class FakeHistoryStore : IGameBuildHistoryStore
    {
        public List<GameBuildObservation> Observations { get; } = [];

        public Task<GameBuildObservation?> GetLatestAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult(Observations.LastOrDefault(x => x.GameId == gameId && x.Provider == provider));

        public Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameBuildObservation>>(Observations.Where(x => x.GameId == gameId && x.Provider == provider).ToArray());
        public Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(IReadOnlyCollection<GameId> gameIds, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameBuildObservation>>(Observations.Where(x => gameIds.Contains(x.GameId) && x.Provider == provider).ToArray());

        public Task<bool> AppendIfChangedAsync(GameBuildObservation observation, CancellationToken cancellationToken)
        {
            if (Observations.LastOrDefault(x => x.GameId == observation.GameId && x.Provider == observation.Provider)?.BuildId == observation.BuildId)
                return Task.FromResult(false);
            Observations.Add(observation);
            return Task.FromResult(true);
        }
    }
}
