using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.Sessions;

public sealed class SessionRuntimeHeartbeatPersistenceTests
{
    private static readonly Guid GameId =
        Guid.Parse("88888888-bbbb-4444-8888-888888888888");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Active_session_is_persisted_again_only_after_five_seconds_have_elapsed()
    {
        var source =
            new QueueProcessSnapshotSource(
                [
                    [MainProcess()],
                    [MainProcess()],
                    [MainProcess()],
                    [MainProcess()]
                ]);

        var store =
            new FakeSessionStore();

        var time =
            new MutableTimeProvider(T0);

        var sut =
            new SessionRuntime(
                source,
                new FakeProcessSignatureStore(
                    Signature()),
                store,
                new ProcessSignatureMatcher(),
                new SessionTransitionPolicy(),
                time);

        await sut.RefreshAsync(
            CancellationToken.None);

        time.SetUtcNow(
            T0.AddSeconds(2));

        await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Single(
            store.Upserts);

        time.SetUtcNow(
            T0.AddSeconds(4));

        await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Single(
            store.Upserts);

        time.SetUtcNow(
            T0.AddSeconds(8));

        await sut.RefreshAsync(
            CancellationToken.None);

        Assert.Equal(
            2,
            store.Upserts.Count);

        var heartbeat =
            store.Upserts[^1];

        Assert.Equal(
            SessionState.Active,
            heartbeat.State);

        Assert.Equal(
            T0.AddSeconds(8),
            heartbeat.LastSeenAtUtc);

        Assert.Equal(
            T0.AddSeconds(8),
            heartbeat.UpdatedAtUtc);
    }

    [Fact]
    public async Task Recovery_uses_last_persisted_heartbeat_instead_of_unpersisted_in_memory_observation()
    {
        var source =
            new QueueProcessSnapshotSource(
                [
                    [MainProcess()],
                    [MainProcess()],
                    [MainProcess()]
                ]);

        var firstStore =
            new FakeSessionStore();

        var time =
            new MutableTimeProvider(T0);

        var firstRuntime =
            new SessionRuntime(
                source,
                new FakeProcessSignatureStore(
                    Signature()),
                firstStore,
                new ProcessSignatureMatcher(),
                new SessionTransitionPolicy(),
                time);

        await firstRuntime.RefreshAsync(
            CancellationToken.None);

        time.SetUtcNow(
            T0.AddSeconds(2));

        await firstRuntime.RefreshAsync(
            CancellationToken.None);

        var lastPersisted =
            Assert.Single(
                firstStore.Upserts);

        time.SetUtcNow(
            T0.AddSeconds(4));

        await firstRuntime.RefreshAsync(
            CancellationToken.None);

        Assert.Single(
            firstStore.Upserts);

        var restartStore =
            new FakeSessionStore(
                lastPersisted);

        var restartedRuntime =
            new SessionRuntime(
                new FixedProcessSnapshotSource([]),
                new FakeProcessSignatureStore(
                    Signature()),
                restartStore,
                new ProcessSignatureMatcher(),
                new SessionTransitionPolicy(),
                new FixedTimeProvider(
                    T0.AddSeconds(30)));

        await restartedRuntime.RefreshAsync(
            CancellationToken.None);

        var recovered =
            Assert.Single(
                restartStore.Upserts);

        Assert.Equal(
            SessionState.Recovered,
            recovered.State);

        Assert.Equal(
            SessionEndReason.RecoveredAfterUnexpectedShutdown,
            recovered.EndReason);

        Assert.Equal(
            lastPersisted.LastSeenAtUtc,
            recovered.ObservedEndedAtUtc);

        Assert.Equal(
            T0.AddSeconds(2),
            recovered.ObservedEndedAtUtc);
    }

    private static ProcessSignature Signature()
        => new(
            GameId,
            [
                new ProcessSignatureEntry(
                    "Game.exe",
                    ProcessSignatureEntryKind.Main)
            ],
            ProcessSignatureOrigin.Manual,
            T0);

    private static ProcessSnapshot MainProcess()
        => new(
            800,
            "Game.exe",
            @"C:\Games\Game.exe",
            T0);

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
            => Task.FromResult(
                _active);

        public Task<IReadOnlyList<GameSession>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<GameSession>>(
                []);
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
