using System.ComponentModel;
using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.Sessions;

public sealed class SessionRuntimeSharedCaptureTests
{
    private static readonly Guid GameId = Guid.Parse("17777777-aaaa-4444-8888-777777777777");
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Capture_is_shared_once_before_matching()
    {
        var f = new Fixture();
        f.Source.Result = new ProcessCaptureResult([new(12, "Game.exe", null, T0)], true);
        f.Observer.AfterObserve = () => f.Source.Result = new ProcessCaptureResult([new(13, "Game.exe", null, T0)], true);
        await f.Refresh();
        Assert.Equal(1, f.Source.Captures);
        Assert.Same(f.Source.FirstResult!.Processes, Assert.Single(f.Observer.Seen).Processes);
        Assert.Equal(T0, Assert.Single(f.Observer.Times));
        Assert.Single((await f.Refresh()).ActiveSessions);
        Assert.Equal(2, f.Source.Captures);
    }

    [Fact]
    public async Task Promotion_only_matches_on_next_cycle()
    {
        var f = new Fixture();
        f.Signatures.Values = [];
        f.Observer.AfterObserve = () => f.Signatures.Values = [Fixture.Signature()];
        Assert.Empty((await f.Refresh()).ActiveSessions);
        f.Clock.Now += TimeSpan.FromSeconds(2);
        Assert.Empty((await f.Refresh()).ActiveSessions);
        f.Clock.Now += TimeSpan.FromSeconds(2);
        var started = Assert.Single((await f.Refresh()).ActiveSessions);
        Assert.Equal(T0.AddSeconds(2), started.ObservedStartedAtUtc);
        Assert.Single(f.Sessions.Writes);
    }

    [Fact]
    public async Task Legacy_source_uses_one_capture()
    {
        var legacy = new LegacySource();
        var f = new Fixture(legacy);
        await f.Refresh();
        Assert.Equal(1, legacy.Captures);
        Assert.True(Assert.Single(f.Observer.Seen).IsComplete);
    }

    [Fact]
    public async Task Incomplete_capture_is_not_known_absence()
    {
        var f = new Fixture();
        await f.Refresh();
        f.Clock.Now += TimeSpan.FromSeconds(2);
        var active = Assert.Single((await f.Refresh()).ActiveSessions);
        f.Source.Result = new ProcessCaptureResult([], false);
        f.Clock.Now += TimeSpan.FromMinutes(2);
        Assert.Equal(active, Assert.Single((await f.Refresh()).ActiveSessions));
        Assert.Single(f.Sessions.Writes);
        f.Source.Result = new ProcessCaptureResult([], true);
        Assert.Empty((await f.Refresh()).ActiveSessions);
        Assert.Equal(active.LastSeenAtUtc, f.Sessions.Writes.Last().ObservedEndedAtUtc);
    }

    [Fact]
    public async Task Cancellation_does_not_match_or_backfill()
    {
        var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        f.Observer.AfterObserve = cancellation.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Refresh(cancellation.Token));
        Assert.Empty(f.Sessions.Writes);
        f.Observer.AfterObserve = null;
        Assert.Empty((await f.Refresh()).ActiveSessions);
    }

    [Fact]
    public async Task Observer_fault_does_not_become_process_absence()
    {
        var f = new Fixture();
        await f.Refresh();
        var active = Assert.Single((await f.Refresh()).ActiveSessions);
        f.Observer.Fault = new InvalidOperationException("observer fault");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Refresh());
        Assert.Single(f.Sessions.Writes);
        f.Observer.Fault = null;
        Assert.Equal(active.SessionId, Assert.Single((await f.Refresh()).ActiveSessions).SessionId);
    }

    [Fact]
    public async Task Capture_failure_marks_gap_and_preserves_active_session()
    {
        var f = new Fixture();
        await f.Refresh();
        var active = Assert.Single((await f.Refresh()).ActiveSessions);
        f.Source.Fault = new InvalidOperationException("capture fault");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Refresh());
        Assert.Equal(1, f.Observer.Gaps);
        f.Source.Fault = null;
        Assert.Equal(active.SessionId, Assert.Single((await f.Refresh()).ActiveSessions).SessionId);
    }

    [Fact]
    public async Task Expected_Windows_capture_fault_is_classified_only_after_gap()
    {
        var f = new Fixture();
        await f.Refresh();
        var active = Assert.Single((await f.Refresh()).ActiveSessions);
        f.Source.Fault = new Win32Exception("Enumeration unavailable.");

        var fault = await Assert.ThrowsAsync<ProcessCaptureUnavailableException>(() => f.Refresh());

        Assert.IsType<Win32Exception>(fault.InnerException);
        Assert.Equal(1, f.Observer.Gaps);
        Assert.Single(f.Sessions.Writes);
        f.Source.Fault = null;
        Assert.Equal(active.SessionId, Assert.Single((await f.Refresh()).ActiveSessions).SessionId);
    }

    [Fact]
    public async Task Pre_capture_Windows_fault_remains_unclassified_and_does_not_mark_gap()
    {
        var f = new Fixture();
        f.Signatures.Fault = new Win32Exception("Revision validation failed.");

        await Assert.ThrowsAsync<Win32Exception>(() => f.Refresh());

        Assert.Equal(0, f.Source.Captures);
        Assert.Equal(0, f.Observer.Gaps);
    }

    [Fact]
    public async Task Gap_callback_fault_does_not_mask_capture_failure()
    {
        var f = new Fixture();
        var failure = new InvalidOperationException("capture failed");
        f.Source.Fault = failure;
        f.Observer.GapFault = new ApplicationException("gap failed");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Refresh());
        Assert.Same(failure, thrown);
        Assert.Equal(1, f.Observer.Gaps);
    }

    [Fact]
    public void Capture_result_defensively_copies_mutable_input()
    {
        var input = new List<ProcessSnapshot> { new(12, "Game.exe", null, T0) };
        var capture = new ProcessCaptureResult(input, true);
        input.Clear();
        Assert.Single(capture.Processes);
        Assert.False(capture.Processes is List<ProcessSnapshot>);
    }

    private sealed class Fixture
    {
        internal readonly Source Source = new();
        internal readonly Observer Observer = new();
        internal readonly SignatureStore Signatures = new();
        internal readonly SessionStore Sessions = new();
        internal readonly Clock Clock = new();
        private readonly SessionRuntime _runtime;

        internal Fixture(IProcessSnapshotSource? processSource = null)
        {
            _runtime = SessionRuntime.CreateWithObserver(processSource ?? Source, Signatures, Sessions,
                new ProcessSignatureMatcher(), new SessionTransitionPolicy(), new Corrections(),
                new SessionCorrectionPolicy(), Clock, Observer);
        }

        internal Task<SessionRuntimeSnapshot> Refresh(CancellationToken ct = default) => _runtime.RefreshAsync(ct);
        internal static ProcessSignature Signature() => new(GameId,
            [new("Game.exe", ProcessSignatureEntryKind.Main)], ProcessSignatureOrigin.Manual, T0);
    }

    private sealed class Source : IProcessSnapshotSource
    {
        internal ProcessCaptureResult Result = new([new(12, "Game.exe", null, T0)], true);
        internal ProcessCaptureResult? FirstResult;
        internal Exception? Fault;
        internal int Captures;
        public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(CancellationToken ct) => throw new InvalidOperationException("quality path required");
        public Task<ProcessCaptureResult> CaptureWithQualityAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Captures++;
            if (Fault is not null) throw Fault;
            FirstResult ??= Result;
            return Task.FromResult(Result);
        }
    }

    private sealed class LegacySource : IProcessSnapshotSource
    {
        internal int Captures;
        public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(CancellationToken ct)
        {
            Captures++;
            return Task.FromResult<IReadOnlyList<ProcessSnapshot>>([new(12, "Game.exe", null, T0)]);
        }
    }

    private sealed class Observer : IProcessCaptureObserver
    {
        internal readonly List<ProcessCaptureResult> Seen = [];
        internal readonly List<DateTimeOffset> Times = [];
        internal Action? AfterObserve;
        internal Exception? Fault;
        internal Exception? GapFault;
        internal int Gaps;
        public Task ObserveAsync(ProcessCaptureResult capture, DateTimeOffset observedAtUtc, CancellationToken ct)
        {
            Seen.Add(capture);
            Times.Add(observedAtUtc);
            AfterObserve?.Invoke();
            ct.ThrowIfCancellationRequested();
            if (Fault is not null) throw Fault;
            return Task.CompletedTask;
        }
        public void MarkCaptureGap()
        {
            Gaps++;
            if (GapFault is not null) throw GapFault;
        }
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = T0;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class SignatureStore : IProcessSignatureStore
    {
        internal IReadOnlyList<ProcessSignature> Values = [Fixture.Signature()];
        internal Exception? Fault;
        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(CancellationToken ct)
        {
            if (Fault is not null) throw Fault;
            return Task.FromResult(Values);
        }
        public Task<ProcessSignature?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Values.FirstOrDefault(s => s.GameId == id));
        public Task UpsertAsync(ProcessSignature s, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class SessionStore : ISessionStore
    {
        internal readonly List<GameSession> Writes = [];
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameSession>>(
                Writes
                    .Where(session => session.GameId == gameId)
                    .OrderByDescending(session => session.ObservedStartedAtUtc)
                    .ThenByDescending(session => session.SessionId)
                    .ToArray());
        public Task<GameSession?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<GameSession?>(null);
        public Task UpsertAsync(GameSession s, CancellationToken ct) { Writes.Add(s); return Task.CompletedTask; }
    }

    private sealed class Corrections : ISessionCorrectionStore
    {
        public Task UpsertAsync(SessionCorrection c, CancellationToken ct) => throw new NotSupportedException();
        public Task<SessionCorrection?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<SessionCorrection?>(null);
    }
}
