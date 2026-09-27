using PlayStead.Core.Sessions;

namespace PlayStead.Data.Sessions;

public sealed class SqliteWeeklyActivitySummaryService : IWeeklyActivitySummaryService
{
    private readonly ISessionStore _sessions;

    public SqliteWeeklyActivitySummaryService(ISessionStore sessions) => _sessions = sessions;

    public async Task<WeeklyActivitySummary> GetAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var localNow = nowUtc.ToLocalTime();
        var monday = localNow.Date.AddDays(-(int)localNow.DayOfWeek + (int)DayOfWeek.Monday);
        var startUtc = new DateTimeOffset(monday, localNow.Offset).ToUniversalTime();
        var sessions = await _sessions.GetRecentAsync(10_000, cancellationToken);
        var completed = sessions.Where(session =>
            session.State is SessionState.Ended or SessionState.Recovered &&
            session.ObservedEndedAtUtc is not null &&
            session.ObservedStartedAtUtc >= startUtc &&
            session.ObservedStartedAtUtc <= nowUtc).ToList();
        var total = completed.Aggregate(TimeSpan.Zero, (sum, session) =>
            sum + (session.ObservedEndedAtUtc!.Value - session.ObservedStartedAtUtc));
        return new WeeklyActivitySummary(total, completed.Count, completed.Select(x => x.GameId).Distinct().Count());
    }
}
