using PlayStead.Core.GameBuildHistory;
using PlayStead.Core.Library;
using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.GameBuildHistory;

public sealed class GameBuildHistoryServiceTests
{
    [Fact]
    public async Task First_observation_is_a_baseline_and_same_build_is_deduplicated()
    {
        var store = new FakeHistoryStore();
        var service = new GameBuildHistoryService(store);
        var observation = Observation("build-a", new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.True(await service.AppendIfChangedAsync(observation, CancellationToken.None));
        Assert.False(await service.AppendIfChangedAsync(observation with { ObservedAtUtc = observation.ObservedAtUtc.AddHours(1) }, CancellationToken.None));
        Assert.Single(await service.GetHistoryAsync(observation.GameId, observation.Provider, CancellationToken.None));
    }

    [Fact]
    public async Task Build_changes_append_as_a_multi_change_sequence()
    {
        var store = new FakeHistoryStore();
        var service = new GameBuildHistoryService(store);
        var first = Observation("build-a", new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        await service.AppendIfChangedAsync(first, CancellationToken.None);
        Assert.True(await service.AppendIfChangedAsync(first with { BuildId = "build-b", ObservedAtUtc = first.ObservedAtUtc.AddDays(1) }, CancellationToken.None));
        Assert.True(await service.AppendIfChangedAsync(first with { BuildId = "build-c", ObservedAtUtc = first.ObservedAtUtc.AddDays(2) }, CancellationToken.None));

        Assert.Equal(3, (await service.GetHistoryAsync(first.GameId, first.Provider, CancellationToken.None)).Count);
        Assert.Equal(2, await service.CountChangesSinceAsync(first.GameId, first.Provider, first.ObservedAtUtc, CancellationToken.None));
    }

    [Fact]
    public async Task Baseline_observed_after_last_completed_play_is_safe()
    {
        var store = new FakeHistoryStore();
        var sessions = new FakeSessionStore();
        var service = new GameBuildHistoryService(store, sessions);
        var gameId = GameId.New();
        var playEnded = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);
        sessions.Sessions.Add(new GameSession(Guid.NewGuid(), gameId.Value, playEnded.AddHours(-1), playEnded, playEnded,
            SessionState.Ended, SessionEndReason.ProcessExited, SessionDetectionSource.ProcessMonitor, playEnded, playEnded));
        var baseline = Observation("build-a", playEnded.AddDays(1)) with { GameId = gameId };
        await service.AppendIfChangedAsync(baseline, CancellationToken.None);
        await service.AppendIfChangedAsync(baseline with { BuildId = "build-b", ObservedAtUtc = playEnded.AddDays(2) }, CancellationToken.None);

        Assert.Equal(1, await service.CountChangesSinceLastPlayAsync(gameId, ProviderKind.Steam, CancellationToken.None));
    }

    [Fact]
    public async Task No_completed_play_history_returns_no_changes()
    {
        var service = new GameBuildHistoryService(new FakeHistoryStore(), new FakeSessionStore());
        Assert.Equal(0, await service.CountChangesSinceLastPlayAsync(GameId.New(), ProviderKind.Steam, CancellationToken.None));
    }

    [Fact]
    public async Task Batch_counts_include_only_installed_games_with_changes()
    {
        var store = new FakeHistoryStore();
        var sessions = new FakeSessionStore();
        var service = new GameBuildHistoryService(store, sessions);
        var changedGame = GameId.New();
        var baselineOnlyGame = GameId.New();
        var ended = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);
        sessions.Sessions.AddRange([Ended(changedGame, ended), Ended(baselineOnlyGame, ended)]);
        var changedBaseline = new GameBuildObservation(changedGame, ProviderKind.Steam, "a", "A", ended.AddDays(1));
        var baseline = new GameBuildObservation(baselineOnlyGame, ProviderKind.Steam, "b", "A", ended.AddDays(1));
        store.Seed(changedBaseline, baseline);
        store.Seed(changedBaseline with { BuildId = "B", ObservedAtUtc = ended.AddDays(2) });

        var counts = await service.CountGamesChangedSinceLastPlayAsync(
            [changedGame, baselineOnlyGame, GameId.New()], ProviderKind.Steam, CancellationToken.None);

        Assert.Equal(1, counts[changedGame.Value]);
        Assert.Equal(0, counts[baselineOnlyGame.Value]);
    }

    private static GameSession Ended(GameId gameId, DateTimeOffset ended) =>
        new(Guid.NewGuid(), gameId.Value, ended.AddHours(-1), ended, ended, SessionState.Ended,
            SessionEndReason.ProcessExited, SessionDetectionSource.ProcessMonitor, ended, ended);

    private static GameBuildObservation Observation(string buildId, DateTimeOffset observedAt) =>
        new(GameId.New(), ProviderKind.Steam, "1284210", buildId, observedAt);

    private sealed class FakeHistoryStore : IGameBuildHistoryStore
    {
        private readonly List<GameBuildObservation> _items = [];
        public void Seed(params GameBuildObservation[] observations) => _items.AddRange(observations);
        public Task<GameBuildObservation?> GetLatestAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult(_items.Where(x => x.GameId == gameId && x.Provider == provider).OrderByDescending(x => x.ObservedAtUtc).FirstOrDefault());
        public Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameBuildObservation>>(_items.Where(x => x.GameId == gameId && x.Provider == provider).OrderBy(x => x.ObservedAtUtc).ToArray());
        public Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(IReadOnlyCollection<GameId> gameIds, ProviderKind provider, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameBuildObservation>>(_items.Where(x => gameIds.Contains(x.GameId) && x.Provider == provider).OrderBy(x => x.ObservedAtUtc).ToArray());
        public Task<bool> AppendIfChangedAsync(GameBuildObservation observation, CancellationToken cancellationToken)
        {
            if (_items.LastOrDefault(x => x.GameId == observation.GameId && x.Provider == observation.Provider)?.BuildId == observation.BuildId)
                return Task.FromResult(false);
            _items.Add(observation);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeSessionStore : ISessionStore
    {
        public List<GameSession> Sessions { get; } = [];
        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken) { Sessions.Add(session); return Task.CompletedTask; }
        public Task<GameSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) => Task.FromResult<GameSession?>(Sessions.FirstOrDefault(x => x.SessionId == sessionId));
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>(Sessions.Where(x => x.ObservedEndedAtUtc is null).ToArray());
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>(Sessions.Take(limit).ToArray());
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>(Sessions.Where(x => x.GameId == gameId).ToArray());
    }
}
