using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;
using PlayStead.Core.Sessions;
using PlayStead.Data.Sessions;

namespace PlayStead.Data.Tests.Sessions;

public sealed class SqliteWeeklyActivitySummaryServiceProviderTests
{
    [Fact]
    public async Task Monday_session_is_included_from_monday_start()
    {
        var monday = Local(2026, 9, 28, 12);
        var summary = await SummaryAtAsync(monday, Local(2026, 9, 28, 9));

        Assert.Equal(1, summary.SessionCount);
    }

    [Fact]
    public async Task Saturday_keeps_the_previous_monday_as_week_start()
    {
        var saturday = Local(2026, 10, 3, 12);
        var summary = await SummaryAtAsync(saturday, Local(2026, 9, 28, 9));

        Assert.Equal(1, summary.SessionCount);
    }

    [Fact]
    public async Task Sunday_keeps_the_current_week_and_does_not_roll_forward()
    {
        var sunday = Local(2026, 10, 4, 12);
        var summary = await SummaryAtAsync(
            sunday,
            Local(2026, 9, 28, 9),
            Local(2026, 10, 4, 9));

        Assert.Equal(2, summary.SessionCount);
    }

    [Fact]
    public async Task Previous_sunday_is_excluded_from_the_current_week()
    {
        var sunday = Local(2026, 10, 4, 12);
        var summary = await SummaryAtAsync(sunday, Local(2026, 9, 27, 9));

        Assert.Equal(0, summary.SessionCount);
    }

    [Fact]
    public async Task Next_monday_is_excluded_from_the_current_week()
    {
        var monday = Local(2026, 9, 28, 12);
        var summary = await SummaryAtAsync(monday, Local(2026, 10, 5, 9));

        Assert.Equal(0, summary.SessionCount);
    }

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

    private static async Task<WeeklyActivitySummary> SummaryAtAsync(
        DateTimeOffset nowLocal,
        params DateTimeOffset[] sessionStartsLocal)
    {
        var sessions = sessionStartsLocal
            .Select(start => new GameSession(
                Guid.NewGuid(),
                Guid.NewGuid(),
                start.ToUniversalTime(),
                start.AddMinutes(30).ToUniversalTime(),
                start.AddMinutes(30).ToUniversalTime(),
                SessionState.Ended,
                SessionEndReason.ProcessExited,
                SessionDetectionSource.ProcessMonitor,
                start.ToUniversalTime(),
                start.AddMinutes(30).ToUniversalTime()))
            .ToArray();
        var service = new SqliteWeeklyActivitySummaryService(new FakeSessionStore(sessions));
        return await service.GetAsync(nowLocal.ToUniversalTime(), CancellationToken.None);
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour) =>
        new(
            new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified),
            TimeZoneInfo.Local.GetUtcOffset(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified)));

    private sealed class FakeProviderObservedSessionStore(ProviderObservedSession session) : IProviderObservedSessionStore
    {
        public Task UpsertAsync(ProviderObservedSession value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ProviderObservedSession>> GetByPeriodAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProviderObservedSession>>([session]);
    }

    private sealed class FakeSessionStore(IReadOnlyList<GameSession>? sessions = null) : ISessionStore
    {
        private readonly IReadOnlyList<GameSession> _sessions = sessions ?? [];

        public Task UpsertAsync(GameSession session, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<GameSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken) => Task.FromResult<GameSession?>(null);
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken cancellationToken) => Task.FromResult(_sessions);
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<GameSession>>([]);
    }
}
