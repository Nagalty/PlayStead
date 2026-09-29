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

    private static EffectiveActivityService CreateService(
        GameId gameId,
        IReadOnlyList<ProviderActivityMetadata> metadata,
        IReadOnlyList<ProviderObservedSession> recovered) =>
        new(
            new MetadataStore(metadata),
            new RecoveredStore(recovered),
            new SessionStore(),
            new FrozenTimeProvider(Now));

    private static ProviderObservedSession Recovered(GameId gameId, TimeSpan duration) =>
        new(
            Guid.NewGuid(),
            gameId,
            ProviderKind.Steam,
            "1",
            Now - duration,
            Now,
            "test",
            ProviderObservedSessionCompleteness.Complete);

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

    private sealed class SessionStore : ISessionStore
    {
        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<GameSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) => Task.FromResult<GameSession?>(null);
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>([]);
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
