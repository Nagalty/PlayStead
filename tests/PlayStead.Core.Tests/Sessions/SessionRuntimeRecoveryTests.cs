using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.Sessions;

public sealed class SessionRuntimeRecoveryTests
{
    private static readonly Guid GameId =
        Guid.Parse("77777777-aaaa-4444-8888-777777777777");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task First_refresh_closes_persisted_active_session_at_last_reliable_timestamp_when_process_is_absent()
    {
        var persisted = ActiveSession(
            sessionId:
                Guid.Parse("70000000-0000-0000-0000-000000000001"),
            lastSeenAtUtc:
                T0.AddSeconds(5));

        var store =
            new FakeSessionStore(persisted);

        var sut = CreateRuntime(
            processSource:
                new FixedProcessSnapshotSource([]),
            signatureStore:
                new FakeProcessSignatureStore(
                    Signature("Game.exe")),
            sessionStore:
                store,
            timeProvider:
                new FixedTimeProvider(
                    T0.AddMinutes(2)));

        var snapshot = await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Empty(
            snapshot.ActiveSessions);

        var recovered = Assert.Single(
            store.Upserts);

        Assert.Equal(
            persisted.SessionId,
            recovered.SessionId);

        Assert.Equal(
            SessionState.Recovered,
            recovered.State);

        Assert.Equal(
            SessionEndReason.RecoveredAfterUnexpectedShutdown,
            recovered.EndReason);

        Assert.Equal(
            persisted.LastSeenAtUtc,
            recovered.LastSeenAtUtc);

        Assert.Equal(
            persisted.LastSeenAtUtc,
            recovered.ObservedEndedAtUtc);

        Assert.Equal(
            T0.AddMinutes(2),
            recovered.UpdatedAtUtc);
    }

    [Fact]
    public async Task First_refresh_continues_same_persisted_session_immediately_when_main_process_is_present()
    {
        var persisted = ActiveSession(
            sessionId:
                Guid.Parse("70000000-0000-0000-0000-000000000002"),
            lastSeenAtUtc:
                T0.AddSeconds(5));

        var store =
            new FakeSessionStore(persisted);

        var now =
            T0.AddMinutes(2);

        var sut = CreateRuntime(
            processSource:
                new FixedProcessSnapshotSource(
                    [
                        new ProcessSnapshot(
                            700,
                            "Game.exe",
                            @"C:\Games\Game.exe",
                            T0)
                    ]),
            signatureStore:
                new FakeProcessSignatureStore(
                    Signature("Game.exe")),
            sessionStore:
                store,
            timeProvider:
                new FixedTimeProvider(now));

        var snapshot = await sut.RefreshAsync(
            CancellationToken.None);

        var active = Assert.Single(
            snapshot.ActiveSessions);

        Assert.Equal(
            persisted.SessionId,
            active.SessionId);

        Assert.Equal(
            persisted.ObservedStartedAtUtc,
            active.ObservedStartedAtUtc);

        Assert.Equal(
            now,
            active.LastSeenAtUtc);

        Assert.Equal(
            SessionState.Active,
            active.State);

        Assert.Null(
            active.ObservedEndedAtUtc);

        Assert.Null(
            active.EndReason);
    }

    [Fact]
    public async Task Persisted_active_sessions_are_loaded_only_once_per_runtime_lifetime()
    {
        var store =
            new FakeSessionStore();

        var source =
            new QueueProcessSnapshotSource(
                [
                    [],
                    []
                ]);

        var sut = CreateRuntime(
            source,
            new FakeProcessSignatureStore(
                Signature("Game.exe")),
            store,
            new FixedTimeProvider(T0));

        await sut.RefreshAsync(
            CancellationToken.None);

        await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Equal(
            1,
            store.GetActiveCallCount);
    }

    private static SessionRuntime CreateRuntime(
        IProcessSnapshotSource processSource,
        IProcessSignatureStore signatureStore,
        ISessionStore sessionStore,
        TimeProvider timeProvider)
        => new(
            processSource,
            signatureStore,
            sessionStore,
            new ProcessSignatureMatcher(),
            new SessionTransitionPolicy(),
            new FakeSessionCorrectionStore(),
            new SessionCorrectionPolicy(),
            timeProvider);

    private static ProcessSignature Signature(
        string executableName)
        => new(
            GameId,
            [
                new ProcessSignatureEntry(
                    executableName,
                    ProcessSignatureEntryKind.Main)
            ],
            ProcessSignatureOrigin.Manual,
            T0);

    private static GameSession ActiveSession(
        Guid sessionId,
        DateTimeOffset lastSeenAtUtc)
        => new(
            sessionId,
            GameId,
            T0,
            lastSeenAtUtc,
            null,
            SessionState.Active,
            null,
            SessionDetectionSource.ProcessMonitor,
            T0,
            lastSeenAtUtc);

    private sealed class FixedProcessSnapshotSource :
        IProcessSnapshotSource
    {
        private readonly IReadOnlyList<ProcessSnapshot> _snapshots;

        public FixedProcessSnapshotSource(
            IReadOnlyList<ProcessSnapshot> snapshots)
        {
            _snapshots = snapshots;
        }

        public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _snapshots);
        }
    }

    private sealed class QueueProcessSnapshotSource :
        IProcessSnapshotSource
    {
        private readonly Queue<IReadOnlyList<ProcessSnapshot>>
            _snapshots;

        public QueueProcessSnapshotSource(
            IEnumerable<IReadOnlyList<ProcessSnapshot>> snapshots)
        {
            _snapshots =
                new Queue<IReadOnlyList<ProcessSnapshot>>(
                    snapshots);
        }

        public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _snapshots.Count == 0
                    ? (IReadOnlyList<ProcessSnapshot>)[]
                    : _snapshots.Dequeue());
        }
    }

    private sealed class FakeProcessSignatureStore :
        IProcessSignatureStore
    {
        private readonly IReadOnlyList<ProcessSignature> _signatures;

        public FakeProcessSignatureStore(
            params ProcessSignature[] signatures)
        {
            _signatures = signatures;
        }

        public Task UpsertAsync(
            ProcessSignature signature,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ProcessSignature?> GetAsync(
            Guid gameId,
            CancellationToken cancellationToken)
            => Task.FromResult(
                _signatures.FirstOrDefault(
                    x => x.GameId == gameId));

        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                _signatures);
        }
    }

    private sealed class FakeSessionStore :
        ISessionStore
    {
        private readonly IReadOnlyList<GameSession> _active;

        public FakeSessionStore(
            params GameSession[] active)
        {
            _active = active;
        }

        public int GetActiveCallCount { get; private set; }

        public List<GameSession> Upserts { get; } = [];

        public Task UpsertAsync(
            GameSession session,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Upserts.Add(session);
            return Task.CompletedTask;
        }

        public Task<GameSession?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken)
            => Task.FromResult<GameSession?>(null);

        public Task<IReadOnlyList<GameSession>> GetActiveAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetActiveCallCount++;

            return Task.FromResult(
                _active);
        }

        public Task<IReadOnlyList<GameSession>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GameSession>>(
                []);
    }


    private sealed class FakeSessionCorrectionStore :
        ISessionCorrectionStore
    {
        public Task UpsertAsync(
            SessionCorrection correction,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<SessionCorrection?> GetAsync(
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<SessionCorrection?>(null);
        }
    }

    private sealed class FixedTimeProvider :
        TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(
            DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
            => _utcNow;
    }
}
