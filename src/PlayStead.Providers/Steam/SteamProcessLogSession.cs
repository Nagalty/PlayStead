namespace PlayStead.Providers.Steam;

public sealed record SteamProcessLogSession(
    string AppId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    bool IsComplete,
    string Source = "SteamProcessLog");

public sealed record SteamProcessLogCoverage(
    DateTimeOffset? EarliestReliableTimestamp,
    DateTimeOffset? LatestReliableTimestamp)
{
    public bool IsWindowComplete(DateTimeOffset startUtc, DateTimeOffset endUtc) =>
        EarliestReliableTimestamp <= startUtc && LatestReliableTimestamp >= endUtc;
}
