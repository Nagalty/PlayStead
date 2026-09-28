using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderActivity;

public enum ProviderObservedSessionCompleteness
{
    Complete = 0,
    Incomplete = 1
}

public sealed record ProviderObservedSession(
    Guid SessionId,
    GameId GameId,
    ProviderKind Provider,
    string ProviderGameId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string Source,
    ProviderObservedSessionCompleteness Completeness);

public interface IProviderObservedSessionStore
{
    Task UpsertAsync(ProviderObservedSession session, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderObservedSession>> GetByPeriodAsync(
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken);
}
