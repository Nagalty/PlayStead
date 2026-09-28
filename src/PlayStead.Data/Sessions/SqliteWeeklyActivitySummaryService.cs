using PlayStead.Core.Sessions;
using PlayStead.Core.ProviderActivity;

namespace PlayStead.Data.Sessions;

public sealed class SqliteWeeklyActivitySummaryService : IWeeklyActivitySummaryService
{
    private readonly ISessionStore _sessions;
    private readonly IProviderObservedSessionStore? _providerSessions;

    public SqliteWeeklyActivitySummaryService(ISessionStore sessions, IProviderObservedSessionStore? providerSessions = null)
    {
        _sessions = sessions;
        _providerSessions = providerSessions;
    }

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
        if (_providerSessions is null)
            return new WeeklyActivitySummary(total, completed.Count, completed.Select(x => x.GameId).Distinct().Count());

        var provider = await _providerSessions.GetByPeriodAsync(startUtc, nowUtc, cancellationToken);
        var intervals = completed
            .Select(session => new ActivityInterval(new PlayStead.Core.Library.GameId(session.GameId), session.ObservedStartedAtUtc, session.ObservedEndedAtUtc!.Value, true))
            .Concat(provider.Where(session => session.StartedAtUtc is not null)
                .Select(session => new ActivityInterval(
                    session.GameId,
                    session.StartedAtUtc!.Value,
                    session.EndedAtUtc ?? nowUtc,
                    session.EndedAtUtc is not null)))
            .Select(interval => interval with
            {
                StartedAtUtc = interval.StartedAtUtc < startUtc ? startUtc : interval.StartedAtUtc,
                EndedAtUtc = interval.EndedAtUtc > nowUtc ? nowUtc : interval.EndedAtUtc
            })
            .Where(interval => interval.EndedAtUtc >= interval.StartedAtUtc)
            .OrderBy(interval => interval.StartedAtUtc)
            .ToArray();

        var merged = new List<ActivityInterval>();
        foreach (var interval in intervals)
        {
            if (merged.Count > 0 && merged[^1].GameId == interval.GameId && interval.StartedAtUtc <= merged[^1].EndedAtUtc)
            {
                var previous = merged[^1];
                merged[^1] = previous with
                {
                    EndedAtUtc = previous.EndedAtUtc >= interval.EndedAtUtc ? previous.EndedAtUtc : interval.EndedAtUtc,
                    IsComplete = previous.IsComplete && interval.IsComplete
                };
            }
            else
            {
                merged.Add(interval);
            }
        }

        var coverage = provider.Count == 0 ? WeeklyActivityCoverage.ObservedOnly : WeeklyActivityCoverage.Partial;
        var mergedTotal = merged.Where(value => value.IsComplete)
            .Aggregate(TimeSpan.Zero, (sum, value) => sum + (value.EndedAtUtc - value.StartedAtUtc));
        return new WeeklyActivitySummary(mergedTotal, merged.Count, merged.Select(value => value.GameId).Distinct().Count(), coverage);
    }

    private sealed record ActivityInterval(PlayStead.Core.Library.GameId GameId, DateTimeOffset StartedAtUtc, DateTimeOffset EndedAtUtc, bool IsComplete);
}
