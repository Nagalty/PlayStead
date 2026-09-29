using PlayStead.Core.Library;
using PlayStead.Core.Sessions;

namespace PlayStead.Core.ProviderActivity;

public sealed class EffectiveActivityService : IEffectiveActivityService
{
    private readonly IProviderActivityMetadataStore _metadata;
    private readonly IProviderObservedSessionStore _recovered;
    private readonly ISessionStore _observed;
    private readonly TimeProvider _clock;

    public EffectiveActivityService(
        IProviderActivityMetadataStore metadata,
        IProviderObservedSessionStore recovered,
        ISessionStore observed,
        TimeProvider? clock = null)
    {
        _metadata = metadata;
        _recovered = recovered;
        _observed = observed;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<EffectiveActivitySnapshot> GetAsync(
        GameId gameId,
        ProviderKind provider,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var metadata = (await _metadata.GetAllAsync(cancellationToken))
            .FirstOrDefault(x => x.GameId == gameId && x.Provider == provider);
        var recovered = (await _recovered.GetByPeriodAsync(
                now.AddYears(-10), now, cancellationToken))
            .Where(x => x.GameId == gameId && x.Provider == provider &&
                        x.StartedAtUtc is not null && x.EndedAtUtc is not null &&
                        x.Completeness == ProviderObservedSessionCompleteness.Complete)
            .Select(x => (x.SessionId, Start: x.StartedAtUtc!.Value, End: x.EndedAtUtc!.Value))
            .Where(x => x.End > x.Start)
            .ToArray();
        var observed = (await _observed.GetByGameAsync(gameId.Value, cancellationToken))
            .Where(x => x.State is SessionState.Ended or SessionState.Recovered &&
                        x.ObservedEndedAtUtc is not null &&
                        x.ObservedEndedAtUtc > x.ObservedStartedAtUtc)
            .Select(x => (x.SessionId, Start: x.ObservedStartedAtUtc, End: x.ObservedEndedAtUtc!.Value))
            .ToArray();

        var observedMerged = Merge(observed.Select(x => (x.SessionId, x.Start, x.End)));
        var recoveredMerged = Merge(recovered);
        var effectiveMerged = Merge(
            recovered.Select(x => (x.SessionId, x.Start, x.End))
                .Concat(observed.Select(x => (x.SessionId, x.Start, x.End))));
        var knownHistoryStart = effectiveMerged.Select(x => (DateTimeOffset?)x.Start).Min();
        var knownHistoryEnd = effectiveMerged.Select(x => (DateTimeOffset?)x.End).Max();
        var providerLast = metadata?.LastPlayedAtUtc;
        var recoveredLast = recoveredMerged.Select(x => (DateTimeOffset?)x.End).Max();
        var observedLast = observedMerged.Select(x => (DateTimeOffset?)x.End).Max();
        var lastCandidates = new[]
        {
            (Value: providerLast, Source: EffectiveActivitySource.ProviderLifetime),
            (Value: recoveredLast, Source: EffectiveActivitySource.ProviderRecoveredSessions),
            (Value: observedLast, Source: EffectiveActivitySource.PlaySteadObservedSessions)
        }.Where(x => x.Value is not null).OrderByDescending(x => x.Value).FirstOrDefault();

        // EffectiveTotalPlayTime is deliberately lifetime-only. Recovered and
        // observed intervals are bounded activity and remain exposed through
        // their dedicated provenance fields below.
        var effectiveTotal = metadata?.TotalPlaytime;
        var totalSource = effectiveTotal is not null
            ? EffectiveActivitySource.ProviderLifetime
            : EffectiveActivitySource.Unknown;
        var coverage = effectiveTotal is not null
            ? EffectiveActivityCoverage.Lifetime
            : EffectiveActivityCoverage.Unknown;

        return new EffectiveActivitySnapshot(
            gameId, effectiveTotal, totalSource, coverage,
            lastCandidates.Value, lastCandidates.Source,
            observedMerged.Aggregate(TimeSpan.Zero, (sum, x) => sum + (x.End - x.Start)), observedMerged.Count,
            recoveredMerged.Aggregate(TimeSpan.Zero, (sum, x) => sum + (x.End - x.Start)), recoveredMerged.Count,
            recoveredMerged.Select(x => (DateTimeOffset?)x.Start).Min(), recoveredMerged.Select(x => (DateTimeOffset?)x.End).Max(), now)
        {
            EffectiveSessionCount = effectiveMerged.Count,
            KnownSessionHistoryStartUtc = knownHistoryStart,
            KnownSessionHistoryEndUtc = knownHistoryEnd,
            // Neither Steam's rolling process log nor PlayStead's observed
            // sessions proves that the period before the first known episode
            // is empty.
            KnownSessionHistoryIsComplete = false
        };
    }

    private static IReadOnlyList<(Guid SessionId, DateTimeOffset Start, DateTimeOffset End)> Merge(
        IEnumerable<(Guid SessionId, DateTimeOffset Start, DateTimeOffset End)> source)
    {
        var result = new List<(Guid, DateTimeOffset, DateTimeOffset)>();
        foreach (var item in source.OrderBy(x => x.Start).ThenBy(x => x.End))
        {
            if (result.Count == 0 || item.Start > result[^1].Item3)
            {
                result.Add(item);
                continue;
            }
            var previous = result[^1];
            result[^1] = (previous.Item1, previous.Item2, previous.Item3 >= item.End ? previous.Item3 : item.End);
        }
        return result;
    }
}
