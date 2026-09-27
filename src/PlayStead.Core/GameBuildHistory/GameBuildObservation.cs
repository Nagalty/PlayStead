using PlayStead.Core.Library;

namespace PlayStead.Core.GameBuildHistory;

public enum GameBuildObservationSource
{
    LocalProvider = 0
}

public sealed record GameBuildObservation(
    GameId GameId,
    ProviderKind Provider,
    string ProviderGameId,
    string BuildId,
    DateTimeOffset ObservedAtUtc,
    GameBuildObservationSource Source = GameBuildObservationSource.LocalProvider);

public interface IGameBuildHistoryStore
{
    Task<GameBuildObservation?> GetLatestAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(IReadOnlyCollection<GameId> gameIds, ProviderKind provider, CancellationToken cancellationToken);
    Task<bool> AppendIfChangedAsync(GameBuildObservation observation, CancellationToken cancellationToken);
}

public sealed class GameBuildHistoryService
{
    private readonly IGameBuildHistoryStore _store;
    private readonly PlayStead.Core.Sessions.ISessionStore? _sessionStore;

    public GameBuildHistoryService(IGameBuildHistoryStore store, PlayStead.Core.Sessions.ISessionStore? sessionStore = null)
    {
        _store = store;
        _sessionStore = sessionStore;
    }

    public Task<GameBuildObservation?> GetLatestAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
        _store.GetLatestAsync(gameId, provider, cancellationToken);

    public Task<IReadOnlyList<GameBuildObservation>> GetHistoryAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
        _store.GetHistoryAsync(gameId, provider, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> CountGamesChangedSinceLastPlayAsync(
        IEnumerable<GameId> gameIds,
        ProviderKind provider,
        CancellationToken cancellationToken)
    {
        var ids = gameIds.Distinct().ToArray();
        var counts = ids.ToDictionary(id => id.Value, _ => 0);
        if (_sessionStore is null || ids.Length == 0) return counts;

        var idSet = ids.Select(id => id.Value).ToHashSet();
        var sessions = await _sessionStore.GetRecentAsync(int.MaxValue, cancellationToken);
        var lastPlays = sessions
            .Where(x => idSet.Contains(x.GameId) && x.ObservedEndedAtUtc is not null)
            .GroupBy(x => x.GameId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(x => x.ObservedEndedAtUtc!.Value));
        if (lastPlays.Count == 0) return counts;

        var history = await _store.GetHistoryAsync(ids, provider, cancellationToken);
        foreach (var group in history.GroupBy(x => x.GameId.Value))
        {
            if (!lastPlays.TryGetValue(group.Key, out var lastPlay)) continue;
            counts[group.Key] = group
                .OrderBy(x => x.ObservedAtUtc)
                .Select((observation, index) => (observation, index))
                .Count(x => x.index > 0 && x.observation.ObservedAtUtc > lastPlay);
        }

        return counts;
    }

    public Task<bool> AppendIfChangedAsync(GameBuildObservation observation, CancellationToken cancellationToken) =>
        _store.AppendIfChangedAsync(observation, cancellationToken);

    public async Task<int> CountChangesSinceLastPlayAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken)
    {
        if (_sessionStore is null) return 0;
        var sessions = await _sessionStore.GetByGameAsync(gameId.Value, cancellationToken);
        var lastPlay = sessions
            .Where(x => x.ObservedEndedAtUtc is not null)
            .Select(x => x.ObservedEndedAtUtc!.Value)
            .OrderByDescending(x => x)
            .FirstOrDefault();
        return lastPlay == default ? 0 : await CountChangesSinceAsync(gameId, provider, lastPlay, cancellationToken);
    }

    public async Task<DateTimeOffset?> GetLastCompletedPlayAtAsync(GameId gameId, CancellationToken cancellationToken)
    {
        if (_sessionStore is null) return null;
        var sessions = await _sessionStore.GetByGameAsync(gameId.Value, cancellationToken);
        return sessions
            .Where(x => x.ObservedEndedAtUtc is not null)
            .Select(x => x.ObservedEndedAtUtc!.Value)
            .OrderByDescending(x => x)
            .Cast<DateTimeOffset?>()
            .FirstOrDefault();
    }

    public async Task<int> CountChangesSinceAsync(GameId gameId, ProviderKind provider, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        var history = await _store.GetHistoryAsync(gameId, provider, cancellationToken);
        return history
            .OrderBy(x => x.ObservedAtUtc)
            .Select((observation, index) => (observation, index))
            .Count(x => x.index > 0 && x.observation.ObservedAtUtc > sinceUtc);
    }

    public async Task<bool> HasChangedSinceLastPlayAsync(GameId gameId, ProviderKind provider, CancellationToken cancellationToken) =>
        await CountChangesSinceLastPlayAsync(gameId, provider, cancellationToken) > 0;
}
