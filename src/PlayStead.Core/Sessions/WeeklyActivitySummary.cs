namespace PlayStead.Core.Sessions;

public sealed record WeeklyActivitySummary(
    TimeSpan TotalPlayTime,
    int SessionCount,
    int DistinctGameCount,
    WeeklyActivityCoverage Coverage = WeeklyActivityCoverage.ObservedOnly)
{
    public static WeeklyActivitySummary Empty { get; } = new(TimeSpan.Zero, 0, 0);
}

public enum WeeklyActivityCoverage
{
    ObservedOnly = 0,
    Partial = 1,
    Complete = 2
}

public interface IWeeklyActivitySummaryService
{
    Task<WeeklyActivitySummary> GetAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken);
}
