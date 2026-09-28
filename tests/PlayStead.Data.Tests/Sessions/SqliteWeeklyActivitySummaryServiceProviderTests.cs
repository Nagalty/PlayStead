using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.Sessions;
using PlayStead.Data.Sessions;

namespace PlayStead.Data.Tests.Sessions;

public sealed class SqliteWeeklyActivitySummaryServiceProviderTests
{
    [Fact]
    public async Task Complete_provider_session_contributes_once_to_weekly_summary()
    {
        var gameId = new GameId(Guid.NewGuid());
        var started = new DateTimeOffset(2026, 9, 28, 7, 52, 13, TimeSpan.Zero);
        var ended = new DateTimeOffset(2026, 9, 28, 9, 51, 27, TimeSpan.Zero);
        var providerSession = new ProviderObservedSession(
            Guid.NewGuid(), gameId, ProviderKind.Steam, "1172710", started, ended,
            "SteamProcessLog", ProviderObservedSessionCompleteness.Complete);
        var service = new SqliteWeeklyActivitySummaryService(
            new FakeSessionStore(), new FakeProviderObservedSessionStore(providerSession));

        var summary = await service.GetAsync(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.Equal(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(14), summary.TotalPlayTime);
        Assert.Equal(1, summary.SessionCount);
        Assert.Equal(WeeklyActivityCoverage.Complete, summary.Coverage);
    }

    private sealed class FakeProviderObservedSessionStore(ProviderObservedSession session) : IProviderObservedSessionStore
    {
        public Task UpsertAsync(ProviderObservedSession value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ProviderObservedSession>> GetByPeriodAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderObservedSession>>([session]);
    }

    private sealed class FakeSessionStore : ISessionStore
    {
        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<GameSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) => Task.FromResult<GameSession?>(null);
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>([]);
    }
}
