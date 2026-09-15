using System.ComponentModel;

namespace PlayStead.Core.Sessions;

public sealed class SessionRuntime : ISessionRuntime
{
    private static readonly TimeSpan HeartbeatPersistenceInterval =
        TimeSpan.FromSeconds(5);

    private readonly IProcessSnapshotSource _processSource;
    private readonly IProcessSignatureStore _signatureStore;
    private readonly ISessionStore _sessionStore;
    private readonly ProcessSignatureMatcher _matcher;
    private readonly SessionTransitionPolicy _transitions;
    private readonly ISessionCorrectionStore _correctionStore;
    private readonly SessionCorrectionPolicy _correctionPolicy;
    private readonly TimeProvider _timeProvider;
    private readonly IDiscoveredSignatureValidator? _discoveredSignatureValidator;
    private readonly IProcessCaptureObserver? _captureObserver;
    private readonly HashSet<Guid> _unresolvedRecovered = [];

    private readonly Dictionary<Guid, PendingObservation>
        _pending = [];

    private readonly Dictionary<Guid, GameSession>
        _active = [];

    private readonly Dictionary<Guid, DateTimeOffset>
        _lastPersistedAtUtc = [];

    private bool _recoveryInitialized;

    public SessionRuntime(
        IProcessSnapshotSource processSource,
        IProcessSignatureStore signatureStore,
        ISessionStore sessionStore,
        ProcessSignatureMatcher matcher,
        SessionTransitionPolicy transitions,
        ISessionCorrectionStore correctionStore,
        SessionCorrectionPolicy correctionPolicy,
        TimeProvider timeProvider,
        IDiscoveredSignatureValidator? discoveredSignatureValidator = null)
    {
        ArgumentNullException.ThrowIfNull(processSource);
        ArgumentNullException.ThrowIfNull(signatureStore);
        ArgumentNullException.ThrowIfNull(sessionStore);
        ArgumentNullException.ThrowIfNull(matcher);
        ArgumentNullException.ThrowIfNull(transitions);
        ArgumentNullException.ThrowIfNull(correctionStore);
        ArgumentNullException.ThrowIfNull(correctionPolicy);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _processSource = processSource;
        _signatureStore = signatureStore;
        _sessionStore = sessionStore;
        _matcher = matcher;
        _transitions = transitions;
        _correctionStore = correctionStore;
        _correctionPolicy = correctionPolicy;
        _timeProvider = timeProvider;
        _discoveredSignatureValidator = discoveredSignatureValidator;
    }

    public static SessionRuntime CreateWithObserver(
        IProcessSnapshotSource processSource,
        IProcessSignatureStore signatureStore,
        ISessionStore sessionStore,
        ProcessSignatureMatcher matcher,
        SessionTransitionPolicy transitions,
        ISessionCorrectionStore correctionStore,
        SessionCorrectionPolicy correctionPolicy,
        TimeProvider timeProvider,
        IProcessCaptureObserver captureObserver,
        IDiscoveredSignatureValidator? discoveredSignatureValidator = null)
    {
        ArgumentNullException.ThrowIfNull(captureObserver);
        return new(processSource, signatureStore, sessionStore, matcher, transitions,
            correctionStore, correctionPolicy, timeProvider, discoveredSignatureValidator,
            captureObserver);
    }

    private SessionRuntime(
        IProcessSnapshotSource processSource,
        IProcessSignatureStore signatureStore,
        ISessionStore sessionStore,
        ProcessSignatureMatcher matcher,
        SessionTransitionPolicy transitions,
        ISessionCorrectionStore correctionStore,
        SessionCorrectionPolicy correctionPolicy,
        TimeProvider timeProvider,
        IDiscoveredSignatureValidator? discoveredSignatureValidator,
        IProcessCaptureObserver captureObserver)
        : this(processSource, signatureStore, sessionStore, matcher, transitions,
            correctionStore, correctionPolicy, timeProvider, discoveredSignatureValidator)
    {
        _captureObserver = captureObserver;
    }

    public async Task<SessionRuntimeSnapshot> RefreshAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await LoadPersistedActiveSessionsOnceAsync(cancellationToken);

        var signatures =
            await _signatureStore.GetAllAsync(
                cancellationToken);

        var usableSignatures = new List<ProcessSignature>();
        var pendingValidation = new HashSet<Guid>();
        foreach (var signature in signatures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (signature.Origin != ProcessSignatureOrigin.Discovered)
            {
                usableSignatures.Add(signature);
                continue;
            }

            var validation = !ProcessSignatureMatcher.IsDiscoveredAdmissible(signature)
                ? DiscoveredSignatureValidationResult.Invalid
                : _discoveredSignatureValidator is null
                    ? DiscoveredSignatureValidationResult.Pending
                    : await _discoveredSignatureValidator.ValidateAsync(signature, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (validation == DiscoveredSignatureValidationResult.Valid)
                usableSignatures.Add(signature);
            else
            {
                _pending.Remove(signature.GameId);
                if (validation == DiscoveredSignatureValidationResult.Pending)
                    pendingValidation.Add(signature.GameId);
            }
        }

        // A deferred validation must resolve before the observation it authorizes.
        ProcessCaptureResult capture;
        try
        {
            capture = await _processSource.CaptureWithQualityAsync(cancellationToken);
        }
        catch (Exception error)
        {
            try
            {
                _captureObserver?.MarkCaptureGap();
            }
            catch
            {
                // The capture failure remains the refresh failure.
            }
            if (error is Win32Exception captureError)
                throw new ProcessCaptureUnavailableException(captureError);
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        var nowUtc = _timeProvider.GetUtcNow();

        if (_captureObserver is not null)
        {
            await _captureObserver.ObserveAsync(capture, nowUtc, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (!capture.IsComplete)
            return ProjectSnapshot(nowUtc);

        var processes = capture.Processes;

        var matchedMainGames =
            new HashSet<Guid>();

        foreach (var signature in usableSignatures)
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

                var updatedActive =
                    heartbeat.Session
                    ?? active;

                _active[signature.GameId] =
                    updatedActive;

                _pending.Remove(
                    signature.GameId);

                if (heartbeat.Kind ==
                        SessionTransitionKind.Heartbeat &&
                    ShouldPersistHeartbeat(
                        signature.GameId,
                        updatedActive.LastSeenAtUtc))
                {
                    await _sessionStore.UpsertAsync(
                        updatedActive,
                        cancellationToken);

                    _lastPersistedAtUtc[
                        signature.GameId] =
                        updatedActive.LastSeenAtUtc;
                }

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

            _lastPersistedAtUtc[
                signature.GameId] =
                current.LastSeenAtUtc;
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
                            gameId) && !pendingValidation.Contains(gameId))
                .ToArray();

        foreach (var gameId in activeToEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var active =
                _active[gameId];

            var reason =
                _unresolvedRecovered.Contains(gameId)
                    ? SessionEndReason.RecoveredAfterUnexpectedShutdown
                    : SessionEndReason.ProcessExited;

            var ended =
                _transitions.End(
                    active,
                    active.LastSeenAtUtc,
                    reason,
                    nowUtc).Session
                ?? throw new InvalidOperationException(
                    "Session end transition did not produce a session.");

            await _sessionStore.UpsertAsync(
                ended,
                cancellationToken);

            _active.Remove(
                gameId);

            _lastPersistedAtUtc.Remove(
                gameId);
            _unresolvedRecovered.Remove(gameId);
        }

        _unresolvedRecovered.ExceptWith(matchedMainGames);

        return ProjectSnapshot(nowUtc);
    }

    private SessionRuntimeSnapshot ProjectSnapshot(DateTimeOffset nowUtc)
        => new(
            nowUtc,
            _active.Values
                .OrderBy(
                    session =>
                        session.ObservedStartedAtUtc)
                .ThenBy(
                    session =>
                        session.SessionId)
                .ToArray());

    public async Task CorrectSessionAsync(
        SessionCorrectionRequest correction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(correction);
        cancellationToken.ThrowIfCancellationRequested();

        if (correction.SessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "A session identity is required to persist a correction.",
                nameof(correction));
        }

        var session =
            await _sessionStore.GetAsync(
                correction.SessionId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Session '{correction.SessionId}' was not found.");

        SessionCorrection persistedCorrection;

        try
        {
            persistedCorrection =
                _correctionPolicy.Create(
                    session,
                    correction,
                    _timeProvider.GetUtcNow());
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidOperationException(
                "The requested correction would produce an invalid session interval.",
                exception);
        }

        await _correctionStore.UpsertAsync(
            persistedCorrection,
            cancellationToken);
    }

    private async Task
        LoadPersistedActiveSessionsOnceAsync(
            CancellationToken cancellationToken)
    {
        if (_recoveryInitialized)
        {
            return;
        }

        var persisted =
            await _sessionStore.GetActiveAsync(
                cancellationToken);

        foreach (var session in persisted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (session.State != SessionState.Active)
            {
                continue;
            }

            _active[session.GameId] =
                session;

            _lastPersistedAtUtc[
                session.GameId] =
                session.LastSeenAtUtc;

            _unresolvedRecovered.Add(
                session.GameId);
        }

        _recoveryInitialized = true;
    }

    private bool ShouldPersistHeartbeat(
        Guid gameId,
        DateTimeOffset lastSeenAtUtc)
    {
        if (!_lastPersistedAtUtc.TryGetValue(
                gameId,
                out var lastPersistedAtUtc))
        {
            return true;
        }

        return lastSeenAtUtc - lastPersistedAtUtc >=
            HeartbeatPersistenceInterval;
    }

    private sealed record PendingObservation(
        DateTimeOffset FirstObservedAtUtc,
        int ConsecutiveCount);
}
