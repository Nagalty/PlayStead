using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderActivity;

public enum EffectiveActivitySource
{
    Unknown = 0,
    ProviderLifetime = 1,
    ProviderRecoveredSessions = 2,
    PlaySteadObservedSessions = 3
}

public enum EffectiveActivityCoverage
{
    Unknown = 0,
    Bounded = 1,
    Lifetime = 2
}

public sealed record EffectiveActivitySnapshot(
    GameId GameId,
    TimeSpan? EffectiveTotalPlayTime,
    EffectiveActivitySource EffectiveTotalPlayTimeSource,
    EffectiveActivityCoverage EffectiveTotalPlayTimeCoverage,
    DateTimeOffset? EffectiveLastPlayedAtUtc,
    EffectiveActivitySource EffectiveLastPlayedSource,
    TimeSpan PlaySteadObservedTime,
    int PlaySteadSessionCount,
    TimeSpan ProviderRecoveredTime,
    int ProviderRecoveredSessionCount,
    DateTimeOffset? ProviderRecoveredCoverageStartUtc,
    DateTimeOffset? ProviderRecoveredCoverageEndUtc,
    DateTimeOffset ObservedAtUtc)
{
    // Count of complete, reconciled episodes across provider-recovered and
    // PlayStead-observed sources. LastPlayed metadata never contributes an episode.
    public int EffectiveSessionCount { get; init; }

    public DateTimeOffset? KnownSessionHistoryStartUtc { get; init; }

    public DateTimeOffset? KnownSessionHistoryEndUtc { get; init; }

    public bool KnownSessionHistoryIsComplete { get; init; }
}
