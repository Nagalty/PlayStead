namespace PlayStead.Core.Sessions;

public sealed class SessionRuntime : ISessionRuntime
{
    private readonly IProcessSnapshotSource _processSource;
    private readonly IProcessSignatureStore _signatureStore;
    private readonly ISessionStore _sessionStore;
    private readonly ProcessSignatureMatcher _matcher;
    private readonly SessionTransitionPolicy _transitions;
    private readonly TimeProvider _timeProvider;

    private readonly Dictionary<Guid, PendingObservation>
        _pending = [];

    private readonly Dictionary<Guid, GameSession>
        _active = [];

    public SessionRuntime(
        IProcessSnapshotSource processSource,
        IProcessSignatureStore signatureStore,
        ISessionStore sessionStore,
        ProcessSignatureMatcher matcher,
        SessionTransitionPolicy transitions,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(processSource);
        ArgumentNullException.ThrowIfNull(signatureStore);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(matcher);
        ArgumentNullException.ThrowIfNull(transitions);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _processSource = processSource;
        _signatureStore = signatureStore;
        _sessionStore = sessionStore;
        _matcher = matcher;
        _transitions = transitions;
        _timeProvider = timeProvider;
    }

    public async Task<SessionRuntimeSnapshot> RefreshAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var nowUtc =
            _timeProvider.GetUtcNow();

        var processes =
            await _processSource.CaptureAsync(
                cancellationToken);

        var signatures =
            await _signatureStore.GetAllAsync(
                cancellationToken);

        var matchedMainGames =
            new HashSet<Guid>();

        foreach (var signature in signatures)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var match =
                _matcher.Match(
                    signature,
                    processes);

            if (!match.HasMainProcess)
            {
                continue;
            }

            matchedMainGames.Add(
                signature.GameId);

            if (_active.TryGetValue(
                    signature.GameId,
                    out var active))
            {
                var heartbeat =
                    _transitions.Heartbeat(
                        active,
                        nowUtc);

                if (heartbeat.Session is not null)
                {
                    _active[signature.GameId] =
                        heartbeat.Session;
                }

                _pending.Remove(
                    signature.GameId);

                continue;
            }

            if (!_pending.TryGetValue(
                    signature.GameId,
                    out var pending))
            {
                _pending[signature.GameId] =
                    new PendingObservation(
                        nowUtc,
                        1);

                continue;
            }

            var nextCount =
                pending.ConsecutiveCount + 1;

            if (nextCount < 2)
            {
                _pending[signature.GameId] =
                    pending with
                    {
                        ConsecutiveCount = nextCount
                    };

                continue;
            }

            var started =
                _transitions.Start(
                    signature.GameId,
                    pending.FirstObservedAtUtc,
                    nowUtc).Session
                ?? throw new InvalidOperationException(
                    "Session start transition did not produce a session.");

            var current =
                _transitions.Heartbeat(
                    started,
                    nowUtc).Session
                ?? started;

            _active[signature.GameId] =
                current;

            _pending.Remove(
                signature.GameId);

            await _sessionStore.UpsertAsync(
                current,
                cancellationToken);
        }

        var pendingToClear =
            _pending.Keys
                .Where(
                    gameId =>
                        !matchedMainGames.Contains(
                            gameId))
                .ToArray();

        foreach (var gameId in pendingToClear)
        {
            _pending.Remove(gameId);
        }

        var activeToEnd =
            _active.Keys
                .Where(
                    gameId =>
                        !matchedMainGames.Contains(
                            gameId))
                .ToArray();

        foreach (var gameId in activeToEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var active =
                _active[gameId];

            var ended =
                _transitions.End(
                    active,
                    active.LastSeenAtUtc,
                    SessionEndReason.ProcessExited,
                    nowUtc).Session
                ?? throw new InvalidOperationException(
                    "Session end transition did not produce a session.");

            await _sessionStore.UpsertAsync(
                ended,
                cancellationToken);

            _active.Remove(
                gameId);
        }

        return new SessionRuntimeSnapshot(
            nowUtc,
            _active.Values
                .OrderBy(
                    session =>
                        session.ObservedStartedAtUtc)
                .ThenBy(
                    session =>
                        session.SessionId)
                .ToArray());
    }

    private sealed record PendingObservation(
        DateTimeOffset FirstObservedAtUtc,
        int ConsecutiveCount);
}
