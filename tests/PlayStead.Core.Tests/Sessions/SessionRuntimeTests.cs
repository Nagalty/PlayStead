using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.Sessions;

public sealed class SessionRuntimeTests
{
    private static readonly Guid GameA =
        Guid.Parse("11111111-aaaa-4444-8888-111111111111");

    private static readonly Guid GameB =
        Guid.Parse("22222222-bbbb-4444-8888-222222222222");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 0, 45, 0, TimeSpan.Zero);

    [Fact]
    public async Task Explicitly_launched_manual_process_creates_one_session_for_stable_game_id()
    {
        var source = new QueueProcessSnapshotSource([
            [MainProcess(777, "007FirstLight.exe", T0)],
            [MainProcess(777, "007FirstLight.exe", T0)]
        ]);
        var sessions = new FakeSessionStore();
        var sut = CreateRuntime(source, new FakeProcessSignatureStore(), sessions, new MutableTimeProvider(T0));
        sut.TrackLaunchedProcess(GameA, 777, T0);

        var first = await sut.RefreshAsync(CancellationToken.None);
        var second = await sut.RefreshAsync(CancellationToken.None);

        Assert.Equal(GameA, Assert.Single(first.ActiveSessions).GameId);
        Assert.Equal(GameA, Assert.Single(second.ActiveSessions).GameId);
        Assert.Single(sessions.Upserts);
    }

    [Fact]
    public async Task Explicitly_launched_manual_process_close_is_persisted_with_positive_duration()
    {
        var source = new QueueProcessSnapshotSource([
            [MainProcess(778, "007FirstLight.exe", T0)],
            []
        ]);
        var sessions = new FakeSessionStore();
        var time = new MutableTimeProvider(T0);
        var sut = CreateRuntime(source, new FakeProcessSignatureStore(), sessions, time);
        sut.TrackLaunchedProcess(GameA, 778, T0);

        await sut.RefreshAsync(CancellationToken.None);
        time.SetUtcNow(T0.AddMinutes(3));
        var ended = await sut.RefreshAsync(CancellationToken.None);

        Assert.Empty(ended.ActiveSessions);
        Assert.Equal(2, sessions.Upserts.Count);
        Assert.Equal(SessionState.Ended, sessions.Upserts[^1].State);
        Assert.Equal(TimeSpan.FromMinutes(3), sessions.Upserts[^1].ObservedEndedAtUtc - sessions.Upserts[^1].ObservedStartedAtUtc);
    }

    [Fact]
    public async Task Already_running_accepted_game_is_reconciled_on_first_refresh()
    {
        var processStarted = T0.AddMinutes(-28);
        var source = new QueueProcessSnapshotSource(
            [[MainProcess(99, "GameA.exe", processStarted)]]);
        var sessions = new FakeSessionStore();
        var sut = CreateRuntime(
            source,
            new FakeProcessSignatureStore(Signature(GameA, "GameA.exe")),
            sessions,
            new MutableTimeProvider(T0));

        var snapshot = await sut.ReconcileRunningProcessesAsync(CancellationToken.None);

        var active = Assert.Single(snapshot.ActiveSessions);
        Assert.Equal(GameA, active.GameId);
        Assert.Equal(processStarted, active.ObservedStartedAtUtc);
        Assert.Single(sessions.Upserts);
    }

    [Fact]
    public async Task Startup_reconciliation_is_idempotent_on_follow_up_poll()
    {
        var process = MainProcess(100, "GameA.exe", T0.AddMinutes(-5));
        var source = new QueueProcessSnapshotSource([[process], [process], [process]]);
        var sessions = new FakeSessionStore();
        var sut = CreateRuntime(
            source,
            new FakeProcessSignatureStore(Signature(GameA, "GameA.exe")),
            sessions,
            new MutableTimeProvider(T0));

        var first = await sut.ReconcileRunningProcessesAsync(CancellationToken.None);
        var second = await sut.RefreshAsync(CancellationToken.None);

        Assert.Single(first.ActiveSessions);
        Assert.Single(second.ActiveSessions);
        Assert.Single(sessions.Upserts);
    }

    [Fact]
    public async Task Startup_reconciliation_ignores_an_unaccepted_process()
    {
        var source = new QueueProcessSnapshotSource(
            [[MainProcess(101, "Unknown.exe", T0.AddMinutes(-2))]]);
        var sessions = new FakeSessionStore();
        var sut = CreateRuntime(
            source,
            new FakeProcessSignatureStore(Signature(GameA, "GameA.exe")),
            sessions,
            new MutableTimeProvider(T0));

        var snapshot = await sut.ReconcileRunningProcessesAsync(CancellationToken.None);

        Assert.Empty(snapshot.ActiveSessions);
        Assert.Empty(sessions.Upserts);
    }

    [Fact]
    public async Task Startup_reconciliation_falls_back_to_first_observed_time_without_process_start_time()
    {
        var source = new QueueProcessSnapshotSource(
            [[MainProcessWithoutStart(102, "GameA.exe")]]);
        var sessions = new FakeSessionStore();
        var sut = CreateRuntime(
            source,
            new FakeProcessSignatureStore(Signature(GameA, "GameA.exe")),
            sessions,
            new MutableTimeProvider(T0));

        var snapshot = await sut.ReconcileRunningProcessesAsync(CancellationToken.None);

        Assert.Equal(T0, Assert.Single(snapshot.ActiveSessions).ObservedStartedAtUtc);
    }

    [Fact]
    public async Task Main_process_starting_after_startup_requires_two_reliable_observations()
    {
        var source = new QueueProcessSnapshotSource(
            [
                [],
                [MainProcess(100, "GameA.exe")],
                [MainProcess(100, "GameA.exe")]
            ]);

        var signatures = new FakeProcessSignatureStore(
            Signature(GameA, "GameA.exe"));

        var sessions = new FakeSessionStore();
        var time = new MutableTimeProvider(T0);

        var sut = CreateRuntime(
            source,
            signatures,
            sessions,
            time);

        await sut.RefreshAsync(
            CancellationToken.None);

        var first = await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Empty(first.ActiveSessions);
        Assert.Empty(sessions.Upserts);

        time.SetUtcNow(T0.AddSeconds(2));

        var second = await sut.RefreshAsync(
            CancellationToken.None);

        var active = Assert.Single(
            second.ActiveSessions);

        Assert.Equal(
            GameA,
            active.GameId);

        Assert.Equal(
            T0,
            active.ObservedStartedAtUtc);

        Assert.Equal(
            T0.AddSeconds(2),
            active.LastSeenAtUtc);

        Assert.Equal(
            SessionState.Active,
            active.State);

        var persisted = Assert.Single(
            sessions.Upserts);

        Assert.Equal(
            active,
            persisted);
    }

    [Fact]
    public async Task One_frame_main_match_does_not_create_a_false_session()
    {
        var source = new QueueProcessSnapshotSource(
            [
                [],
                [MainProcess(101, "GameA.exe")],
                []
            ]);

        var sessions = new FakeSessionStore();
        var time = new MutableTimeProvider(T0);

        var sut = CreateRuntime(
            source,
            new FakeProcessSignatureStore(
                Signature(GameA, "GameA.exe")),
            sessions,
            time);

        await sut.RefreshAsync(
            CancellationToken.None);

        time.SetUtcNow(T0.AddSeconds(2));

        var snapshot = await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Empty(snapshot.ActiveSessions);
        Assert.Empty(sessions.Upserts);
    }

    [Fact]
    public async Task Multiple_games_can_become_active_in_the_same_refresh()
    {
        var both = new ProcessSnapshot[]
        {
            MainProcess(201, "GameA.exe"),
            MainProcess(202, "GameB.exe")
        };

        var source = new QueueProcessSnapshotSource(
            [
                [],
                both,
                both
            ]);

        var sessions = new FakeSessionStore();
        var time = new MutableTimeProvider(T0);

        var sut = CreateRuntime(
            source,
            new FakeProcessSignatureStore(
                Signature(GameA, "GameA.exe"),
                Signature(GameB, "GameB.exe")),
            sessions,
            time);

        await sut.RefreshAsync(
            CancellationToken.None);

        await sut.RefreshAsync(
            CancellationToken.None);

        time.SetUtcNow(T0.AddSeconds(2));

        var snapshot = await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Equal(
            2,
            snapshot.ActiveSessions.Count);

        Assert.Contains(
            snapshot.ActiveSessions,
            x => x.GameId == GameA);

        Assert.Contains(
            snapshot.ActiveSessions,
            x => x.GameId == GameB);

        Assert.Equal(
            2,
            sessions.Upserts.Count);
    }

    [Fact]
    public async Task Active_session_ends_at_last_reliable_main_observation()
    {
        var source = new QueueProcessSnapshotSource(
            [
                [],
                [MainProcess(301, "GameA.exe")],
                [MainProcess(301, "GameA.exe")],
                [MainProcess(301, "GameA.exe")],
                []
            ]);

        var sessions = new FakeSessionStore();
        var time = new MutableTimeProvider(T0);

        var sut = CreateRuntime(
            source,
            new FakeProcessSignatureStore(
                Signature(GameA, "GameA.exe")),
            sessions,
            time);

        await sut.RefreshAsync(
            CancellationToken.None);

        await sut.RefreshAsync(
            CancellationToken.None);

        time.SetUtcNow(T0.AddSeconds(2));
        await sut.RefreshAsync(
            CancellationToken.None);

        time.SetUtcNow(T0.AddSeconds(4));
        var stillActive = await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Equal(
            T0.AddSeconds(4),
            Assert.Single(stillActive.ActiveSessions).LastSeenAtUtc);

        time.SetUtcNow(T0.AddSeconds(6));
        var endedSnapshot = await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Empty(
            endedSnapshot.ActiveSessions);

        Assert.Equal(
            2,
            sessions.Upserts.Count);

        var ended = sessions.Upserts[^1];

        Assert.Equal(
            SessionState.Ended,
            ended.State);

        Assert.Equal(
            SessionEndReason.ProcessExited,
            ended.EndReason);

        Assert.Equal(
            T0.AddSeconds(4),
            ended.LastSeenAtUtc);

        Assert.Equal(
            T0.AddSeconds(4),
            ended.ObservedEndedAtUtc);
    }

    [Fact]
    public async Task Auxiliary_only_match_never_starts_a_session()
    {
        var signature = new ProcessSignature(
            GameA,
            [
                new ProcessSignatureEntry(
                    "Launcher.exe",
                    ProcessSignatureEntryKind.Auxiliary)
            ],
            ProcessSignatureOrigin.Manual,
            T0);

        var source = new QueueProcessSnapshotSource(
            [
                [MainProcess(401, "Launcher.exe")],
                [MainProcess(401, "Launcher.exe")]
            ]);

        var sessions = new FakeSessionStore();
        var time = new MutableTimeProvider(T0);

        var sut = CreateRuntime(
            source,
            new FakeProcessSignatureStore(signature),
            sessions,
            time);

        await sut.RefreshAsync(
            CancellationToken.None);

        time.SetUtcNow(T0.AddSeconds(2));

        var snapshot = await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Empty(snapshot.ActiveSessions);
        Assert.Empty(sessions.Upserts);
    }

    private static SessionRuntime CreateRuntime(
        IProcessSnapshotSource source,
        IProcessSignatureStore signatures,
        ISessionStore sessions,
        TimeProvider timeProvider)
        => new(
            source,
            signatures,
            sessions,
            new ProcessSignatureMatcher(),
            new SessionTransitionPolicy(),
            new FakeSessionCorrectionStore(),
            new SessionCorrectionPolicy(),
            timeProvider);

    private static ProcessSignature Signature(
        Guid gameId,
        string executableName)
        => new(
            gameId,
            [
                new ProcessSignatureEntry(
                    executableName,
                    ProcessSignatureEntryKind.Main)
            ],
            ProcessSignatureOrigin.Manual,
            T0);

    private static ProcessSnapshot MainProcess(
        int processId,
        string executableName,
        DateTimeOffset? startedAtUtc = null)
        => new(
            processId,
            executableName,
            $@"C:\Games\{executableName}",
            startedAtUtc ?? T0);

    private static ProcessSnapshot MainProcessWithoutStart(
        int processId,
        string executableName)
        => new(
            processId,
            executableName,
            $@"C:\Games\{executableName}",
            null);

    private sealed class QueueProcessSnapshotSource :
        IProcessSnapshotSource
    {
        private readonly Queue<IReadOnlyList<ProcessSnapshot>> _snapshots;

        public QueueProcessSnapshotSource(
            IEnumerable<IReadOnlyList<ProcessSnapshot>> snapshots)
        {
            _snapshots = new Queue<IReadOnlyList<ProcessSnapshot>>(
                snapshots);
        }

        public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_snapshots.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<ProcessSnapshot>>(
                    []);
            }

            return Task.FromResult(
                _snapshots.Dequeue());
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
            => Task.FromResult<IReadOnlyList<GameSession>>(
                []);

        public Task<IReadOnlyList<GameSession>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GameSession>>(
                []);

        public Task<IReadOnlyList<GameSession>> GetByGameAsync(
            Guid gameId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<IReadOnlyList<GameSession>>(
                []);
        }
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

    private sealed class MutableTimeProvider :
        TimeProvider
    {
        private DateTimeOffset _utcNow;

        public MutableTimeProvider(
            DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
            => _utcNow;

        public void SetUtcNow(
            DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }
    }
}
