namespace PlayStead.Core.Sessions;

public sealed record WeeklyActivitySummary(
    TimeSpan TotalPlayTime,
    int SessionCount,
    int DistinctGameCount)
{
    public static WeeklyActivitySummary Empty { get; } = new(TimeSpan.Zero, 0, 0);
}

public interface IWeeklyActivitySummaryService
{
    Task<WeeklyActivitySummary> GetAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken);
}
