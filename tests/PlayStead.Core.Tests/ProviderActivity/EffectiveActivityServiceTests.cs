using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.ProviderActivity;

public sealed class EffectiveActivityServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Bounded_recovered_activity_never_becomes_lifetime_total()
    {
        var gameId = GameId.New();
        var service = CreateService(
            gameId,
            metadata: [],
            recovered: [Recovered(gameId, TimeSpan.FromMinutes(30))]);

        var snapshot = await service.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Null(snapshot.EffectiveTotalPlayTime);
        Assert.Equal(EffectiveActivitySource.Unknown, snapshot.EffectiveTotalPlayTimeSource);
        Assert.Equal(EffectiveActivityCoverage.Unknown, snapshot.EffectiveTotalPlayTimeCoverage);
        Assert.Equal(TimeSpan.FromMinutes(30), snapshot.ProviderRecoveredTime);
    }

    [Fact]
    public async Task Manual_game_projects_complete_PlayStead_time_as_observed_total()
    {
        var gameId = GameId.New();
        var observed = new[]
        {
            Ended(gameId.Value, Now.AddHours(-2), TimeSpan.FromMinutes(30)),
            Ended(gameId.Value, Now.AddHours(-1), TimeSpan.FromMinutes(20))
        };
        var snapshot = await CreateService(gameId, observed: observed).GetAsync(
            gameId, ProviderKind.Manual, CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(50), snapshot.EffectiveTotalPlayTime);
        Assert.Equal(EffectiveActivitySource.PlaySteadObservedSessions, snapshot.EffectiveTotalPlayTimeSource);
        Assert.Equal(EffectiveActivityCoverage.Bounded, snapshot.EffectiveTotalPlayTimeCoverage);
        Assert.Equal(TimeSpan.FromMinutes(50), snapshot.PlaySteadObservedTime);
        Assert.Equal(2, snapshot.EffectiveSessionCount);
    }

    [Fact]
    public async Task Manual_game_without_sessions_remains_unknown()
    {
        var gameId = GameId.New();
        var snapshot = await CreateService(gameId).GetAsync(
            gameId, ProviderKind.Manual, CancellationToken.None);

        Assert.Null(snapshot.EffectiveTotalPlayTime);
        Assert.Equal(EffectiveActivitySource.Unknown, snapshot.EffectiveTotalPlayTimeSource);
        Assert.Equal(TimeSpan.Zero, snapshot.PlaySteadObservedTime);
        Assert.Equal(0, snapshot.EffectiveSessionCount);
    }

    [Fact]
    public async Task Provider_lifetime_total_remains_authoritative_over_bounded_activity()
    {
        var gameId = GameId.New();
        var service = CreateService(
            gameId,
            metadata: [new ProviderActivityMetadata(
                gameId,
                ProviderKind.Steam,
                "1",
                TimeSpan.FromHours(96.9),
                Now.AddMinutes(-5),
                Now,
                ProviderActivityAvailability.Complete)],
            recovered: [Recovered(gameId, TimeSpan.FromMinutes(30))]);

        var snapshot = await service.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Equal(TimeSpan.FromHours(96.9), snapshot.EffectiveTotalPlayTime);
        Assert.Equal(EffectiveActivitySource.ProviderLifetime, snapshot.EffectiveTotalPlayTimeSource);
        Assert.Equal(EffectiveActivityCoverage.Lifetime, snapshot.EffectiveTotalPlayTimeCoverage);
        Assert.Equal(TimeSpan.FromMinutes(30), snapshot.ProviderRecoveredTime);
    }

    [Fact]
    public async Task Effective_session_count_includes_recovered_episodes()
    {
        var gameId = GameId.New();
        var service = CreateService(
            gameId,
            recovered:
            [
                Recovered(gameId, TimeSpan.FromMinutes(30)),
                Recovered(gameId, TimeSpan.FromMinutes(20), Now.AddHours(-3)),
                Recovered(gameId, TimeSpan.FromMinutes(15), Now.AddHours(-6))
            ]);

        var snapshot = await service.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Equal(3, snapshot.EffectiveSessionCount);
    }

    [Fact]
    public async Task Known_history_start_uses_the_earliest_recovered_episode()
    {
        var gameId = GameId.New();
        var earliestEnd = Now.AddHours(-4);
        var service = CreateService(
            gameId,
            recovered: [Recovered(gameId, TimeSpan.FromMinutes(30), earliestEnd)]);

        var snapshot = await service.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Equal(earliestEnd.AddMinutes(-30), snapshot.KnownSessionHistoryStartUtc);
        Assert.False(snapshot.KnownSessionHistoryIsComplete);
    }

    [Fact]
    public async Task Known_history_start_uses_the_earliest_playstead_episode()
    {
        var gameId = GameId.New();
        var started = Now.AddHours(-5);
        var service = CreateService(
            gameId,
            observed: [Ended(gameId.Value, started, TimeSpan.FromMinutes(20))]);

        var snapshot = await service.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Equal(started, snapshot.KnownSessionHistoryStartUtc);
        Assert.False(snapshot.KnownSessionHistoryIsComplete);
    }

    [Fact]
    public async Task Known_history_start_uses_the_earliest_of_both_sources()
    {
        var gameId = GameId.New();
        var recoveredEnd = Now.AddHours(-2);
        var observedStart = Now.AddHours(-5);
        var service = CreateService(
            gameId,
            recovered: [Recovered(gameId, TimeSpan.FromMinutes(30), recoveredEnd)],
            observed: [Ended(gameId.Value, observedStart, TimeSpan.FromMinutes(20))]);

        var snapshot = await service.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Equal(observedStart, snapshot.KnownSessionHistoryStartUtc);
    }

    [Fact]
    public async Task No_known_sessions_have_no_history_coverage_start()
    {
        var gameId = GameId.New();
        var snapshot = await CreateService(gameId).GetAsync(
            gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Null(snapshot.KnownSessionHistoryStartUtc);
    }

    [Fact]
    public async Task Effective_session_count_deduplicates_overlapping_recovered_and_observed_episodes()
    {
        var gameId = GameId.New();
        var start = Now.AddHours(-2);
        var recovered = Recovered(gameId, TimeSpan.FromHours(1), start.AddHours(1));
        var observed = Ended(gameId.Value, start.AddMinutes(15), TimeSpan.FromMinutes(30));
        var service = CreateService(gameId, recovered: [recovered], observed: [observed]);

        var snapshot = await service.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Equal(1, snapshot.EffectiveSessionCount);
    }

    [Fact]
    public async Task Effective_session_count_keeps_distinct_recovered_and_observed_episodes()
    {
        var gameId = GameId.New();
        var recovered = Recovered(gameId, TimeSpan.FromHours(1), Now.AddHours(-3));
        var observed = Ended(gameId.Value, Now.AddHours(-1), TimeSpan.FromMinutes(30));
        var service = CreateService(gameId, recovered: [recovered], observed: [observed]);

        var snapshot = await service.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Equal(2, snapshot.EffectiveSessionCount);
    }

    [Fact]
    public async Task Last_played_metadata_does_not_synthesize_a_session()
    {
        var gameId = GameId.New();
        var service = CreateService(
            gameId,
            metadata: [new ProviderActivityMetadata(
                gameId, ProviderKind.Steam, "1", null, Now, Now,
                ProviderActivityAvailability.Partial)]);

        var snapshot = await service.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Equal(0, snapshot.EffectiveSessionCount);
    }

    [Fact]
    public async Task Incomplete_recovered_episode_does_not_increment_effective_count()
    {
        var gameId = GameId.New();
        var incomplete = new ProviderObservedSession(
            Guid.NewGuid(), gameId, ProviderKind.Steam, "1",
            Now.AddHours(-1), null, "test", ProviderObservedSessionCompleteness.Incomplete);
        var service = CreateService(gameId, recovered: [incomplete]);

        var snapshot = await service.GetAsync(gameId, ProviderKind.Steam, CancellationToken.None);

        Assert.Equal(0, snapshot.EffectiveSessionCount);
    }

    private static EffectiveActivityService CreateService(
        GameId gameId,
        IReadOnlyList<ProviderActivityMetadata>? metadata = null,
        IReadOnlyList<ProviderObservedSession>? recovered = null,
        IReadOnlyList<GameSession>? observed = null) =>
        new(
            new MetadataStore(metadata ?? []),
            new RecoveredStore(recovered ?? []),
            new SessionStore(observed ?? []),
            new FrozenTimeProvider(Now));

    private static ProviderObservedSession Recovered(
        GameId gameId,
        TimeSpan duration,
        DateTimeOffset? endedAt = null) =>
        new(
            Guid.NewGuid(),
            gameId,
            ProviderKind.Steam,
            "1",
            (endedAt ?? Now) - duration,
            endedAt ?? Now,
            "test",
            ProviderObservedSessionCompleteness.Complete);

    private static GameSession Ended(Guid gameId, DateTimeOffset startedAt, TimeSpan duration) =>
        new(
            Guid.NewGuid(), gameId, startedAt, startedAt + duration, startedAt + duration,
            SessionState.Ended, SessionEndReason.ProcessExited, SessionDetectionSource.ProcessMonitor,
            startedAt, startedAt + duration);

    private sealed class MetadataStore(IReadOnlyList<ProviderActivityMetadata> values) : IProviderActivityMetadataStore
    {
        public Task<IReadOnlyList<ProviderActivityMetadata>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult(values);
        public Task UpsertAsync(ProviderActivityMetadata metadata, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecoveredStore(IReadOnlyList<ProviderObservedSession> values) : IProviderObservedSessionStore
    {
        public Task<IReadOnlyList<ProviderObservedSession>> GetByPeriodAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken) => Task.FromResult(values);
        public Task UpsertAsync(ProviderObservedSession session, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SessionStore(IReadOnlyList<GameSession> sessions) : ISessionStore
    {
        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<GameSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) => Task.FromResult<GameSession?>(null);
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GameSession>>(sessions.Where(x => x.GameId == gameId).ToArray());
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
