using PlayStead.Core.Sessions;
using PlayStead.Core.Tests.Sessions.Discovery;
using PlayStead.Core.Sessions.Discovery;

namespace PlayStead.Core.Tests.Sessions;

public sealed class SessionRuntimeDiscoveredSignatureTests
{
    [Fact]
    public void Validator_is_optional_after_time_provider()
    {
        var parameters = Assert.Single(typeof(SessionRuntime).GetConstructors()).GetParameters();
        Assert.Equal(9, parameters.Length);
        Assert.Equal(typeof(TimeProvider), parameters[^2].ParameterType);
        Assert.Equal("IDiscoveredSignatureValidator", parameters[^1].ParameterType.Name);
        Assert.True(parameters[^1].IsOptional);
        Assert.Null(parameters[^1].DefaultValue);
    }

    [Fact]
    public async Task Valid_path_starts_after_two_future_snapshots_and_never_backfills_learning_dates()
    {
        var f = new RuntimeFixture();
        Assert.Empty((await f.Refresh()).ActiveSessions);
        f.Clock.Now = ProcessSignaturePathMatcherTests.T0.AddDays(10).AddSeconds(1);
        var active = Assert.Single((await f.Refresh()).ActiveSessions);
        Assert.Equal(ProcessSignaturePathMatcherTests.T0.AddDays(10), active.ObservedStartedAtUtc);
        Assert.Equal(f.Clock.Now, active.LastSeenAtUtc);
        Assert.Equal(2, f.Source.Captures);
    }

    [Fact]
    public async Task Wrong_path_does_not_start()
    {
        var f = new RuntimeFixture();
        f.Source.Processes = [new(12, "Game.exe", @"C:\Games\Two\Game.exe", ProcessSignaturePathMatcherTests.T0)];
        await f.Refresh();
        Assert.Empty((await f.Refresh()).ActiveSessions);
        Assert.Empty(f.Sessions.Writes);
    }

    [Theory]
    [InlineData(ProcessSignatureOrigin.Manual)]
    [InlineData(ProcessSignatureOrigin.BuiltIn)]
    public async Task Legacy_explicit_origins_still_start(ProcessSignatureOrigin origin)
    {
        var f = new RuntimeFixture();
        f.Signatures.Values = [f.Discovery.Input with { Origin = origin, Discovery = null,
            Entries = [new("Game.exe", ProcessSignatureEntryKind.Main)] }];
        await f.Refresh();
        Assert.Single((await f.Refresh()).ActiveSessions);
        Assert.Empty(f.Discovery.Reads);
    }

    [Fact]
    public async Task Legacy_discovered_does_not_start()
    {
        var f = new RuntimeFixture();
        f.Signatures.Values = [f.Discovery.Input with { Discovery = null }];
        await f.Refresh();
        Assert.Empty((await f.Refresh()).ActiveSessions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Pending_recovery_is_not_closed_or_heartbeated(bool processPresent)
    {
        var f = new RuntimeFixture(recovered: true);
        f.Discovery.Current = null;
        if (!processPresent) f.Source.Processes = [];
        for (var i = 0; i < 3; i++) {
            f.Clock.Now += TimeSpan.FromSeconds(10);
            var active = Assert.Single((await f.Refresh()).ActiveSessions);
            Assert.Equal(f.Persisted, active);
        }
        Assert.Empty(f.Sessions.Writes);
        Assert.Equal(1, f.Sessions.Loads);
    }

    [Fact]
    public async Task Pending_then_valid_uses_fresh_single_capture_and_current_transition_time()
    {
        var f = new RuntimeFixture(recovered: true);
        var ready = f.Discovery.Current;
        f.Discovery.Current = null;
        await f.Refresh();
        f.Discovery.Current = ready;
        f.Source.Processes = [];
        f.Discovery.AfterRead = () => {
            f.Clock.Now += TimeSpan.FromHours(1);
            f.Source.Processes = [RuntimeFixture.Process()];
        };
        var active = Assert.Single((await f.Refresh()).ActiveSessions);
        Assert.Equal(f.Persisted!.SessionId, active.SessionId);
        Assert.Equal(f.Clock.Now, active.LastSeenAtUtc);
        Assert.Equal(2, f.Source.Captures);
    }

    [Fact]
    public async Task Pending_then_invalid_closes_at_persisted_last_seen()
    {
        var f = new RuntimeFixture(recovered: true);
        var ready = f.Discovery.Current;
        f.Discovery.Current = null;
        await f.Refresh();
        await f.Refresh();
        f.Discovery.Current = ready;
        f.Discovery.Revision = new(new FileRevision(11, ProcessSignaturePathMatcherTests.T0), null);
        Assert.Empty((await f.Refresh()).ActiveSessions);
        var closed = Assert.Single(f.Sessions.Writes);
        Assert.Equal(f.Persisted!.LastSeenAtUtc, closed.ObservedEndedAtUtc);
        Assert.Equal(SessionEndReason.RecoveredAfterUnexpectedShutdown, closed.EndReason);
    }

    [Fact]
    public async Task Cancellation_during_pending_keeps_recoverable_state()
    {
        var f = new RuntimeFixture(recovered: true);
        using var cts = new CancellationTokenSource();
        f.Discovery.AfterRead = cts.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Refresh(cts.Token));
        Assert.Empty(f.Sessions.Writes);
        Assert.Equal(0, f.Source.Captures);
        f.Discovery.AfterRead = null;
        f.Discovery.Current = null;
        Assert.Equal(f.Persisted, Assert.Single((await f.Refresh()).ActiveSessions));
        f.Discovery.Current = new DiscoveredSignatureValidatorTests.Fixture().Current;
        Assert.Empty((await f.Refresh()).ActiveSessions);
        var closed = Assert.Single(f.Sessions.Writes);
        Assert.Equal(SessionEndReason.RecoveredAfterUnexpectedShutdown, closed.EndReason);
        Assert.Equal(f.Persisted!.LastSeenAtUtc, closed.ObservedEndedAtUtc);
        Assert.Equal(1, f.Sessions.Loads);
    }

    [Fact]
    public async Task Explicit_session_progresses_while_discovered_is_pending()
    {
        var f = new RuntimeFixture(recovered: true);
        f.Discovery.Current = null;
        var explicitId = Guid.NewGuid();
        f.Signatures.Values = [f.Discovery.Input, new(explicitId,
            [new("Explicit.exe", ProcessSignatureEntryKind.Main)], ProcessSignatureOrigin.Manual, ProcessSignaturePathMatcherTests.T0)];
        f.Source.Processes = [RuntimeFixture.Process(), new(13, "Explicit.exe", null, ProcessSignaturePathMatcherTests.T0)];
        await f.Refresh();
        var snapshot = await f.Refresh();
        Assert.Equal(2, snapshot.ActiveSessions.Count);
        Assert.Equal(f.Persisted, snapshot.ActiveSessions.Single(s => s.GameId == f.Discovery.Input.GameId));
        Assert.Equal(explicitId, Assert.Single(f.Sessions.Writes).GameId);
    }

    [Fact]
    public async Task Revision_failure_closes_active_at_last_reliable_time()
    {
        var f = new RuntimeFixture();
        await f.Refresh();
        f.Clock.Now += TimeSpan.FromSeconds(1);
        var active = Assert.Single((await f.Refresh()).ActiveSessions);
        f.Clock.Now += TimeSpan.FromMinutes(5);
        f.Discovery.Revision = new(null, new(ProcessSignaturePathMatcherTests.Path, InventoryIssueKind.AccessDenied));
        Assert.Empty((await f.Refresh()).ActiveSessions);
        var closed = f.Sessions.Writes.Last();
        Assert.Equal(active.LastSeenAtUtc, closed.ObservedEndedAtUtc);
        Assert.Equal(SessionEndReason.ProcessExited, closed.EndReason);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_validator_cannot_bypass_validation(bool recovered)
    {
        var f = new RuntimeFixture(recovered, validator: false);
        await f.Refresh();
        var snapshot = await f.Refresh();
        if (recovered) Assert.Equal(f.Persisted, Assert.Single(snapshot.ActiveSessions));
        else Assert.Empty(snapshot.ActiveSessions);
        Assert.Empty(f.Sessions.Writes);
    }

    [Fact]
    public async Task Pending_clears_unconfirmed_start_candidate()
    {
        var f = new RuntimeFixture();
        var ready = f.Discovery.Current;
        await f.Refresh();
        f.Discovery.Current = null;
        await f.Refresh();
        f.Discovery.Current = ready;
        f.Clock.Now += TimeSpan.FromSeconds(20);
        Assert.Empty((await f.Refresh()).ActiveSessions);
        var first = f.Clock.Now;
        f.Clock.Now += TimeSpan.FromSeconds(1);
        Assert.Equal(first, Assert.Single((await f.Refresh()).ActiveSessions).ObservedStartedAtUtc);
    }

    private sealed class RuntimeFixture
    {
        internal DiscoveredSignatureValidatorTests.Fixture Discovery = new();
        internal SignatureStore Signatures;
        internal SessionStore Sessions = new();
        internal Source Source = new();
        internal Clock Clock = new();
        internal GameSession? Persisted;
        private readonly SessionRuntime _runtime;
        internal RuntimeFixture(bool recovered = false, bool validator = true)
        {
            Signatures = new() { Values = [Discovery.Input] };
            if (recovered) {
                Persisted = new(Guid.NewGuid(), Discovery.Input.GameId, ProcessSignaturePathMatcherTests.T0,
                    ProcessSignaturePathMatcherTests.T0.AddSeconds(5), null, SessionState.Active, null,
                    SessionDetectionSource.ProcessMonitor, ProcessSignaturePathMatcherTests.T0, ProcessSignaturePathMatcherTests.T0.AddSeconds(5));
                Sessions.Persisted = [Persisted];
            }
            _runtime = new(Source, Signatures, Sessions, new ProcessSignatureMatcher(), new SessionTransitionPolicy(),
                new Corrections(), new SessionCorrectionPolicy(), Clock, validator ? Discovery.Validator() : null);
        }
        internal Task<SessionRuntimeSnapshot> Refresh(CancellationToken ct = default) => _runtime.RefreshAsync(ct);
        internal static ProcessSnapshot Process() => new(12, "Game.exe", ProcessSignaturePathMatcherTests.Path, ProcessSignaturePathMatcherTests.T0);
    }
    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = ProcessSignaturePathMatcherTests.T0.AddDays(10);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Source : IProcessSnapshotSource
    {
        internal IReadOnlyList<ProcessSnapshot> Processes = [RuntimeFixture.Process()];
        internal int Captures;
        public Task<IReadOnlyList<ProcessSnapshot>> CaptureAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Captures++;
            return Task.FromResult(Processes);
        }
    }
    private sealed class SignatureStore : IProcessSignatureStore
    {
        internal IReadOnlyList<ProcessSignature> Values = [];
        public Task<IReadOnlyList<ProcessSignature>> GetAllAsync(CancellationToken ct) => Task.FromResult(Values);
        public Task<ProcessSignature?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Values.FirstOrDefault(s => s.GameId == id));
        public Task UpsertAsync(ProcessSignature s, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class SessionStore : ISessionStore
    {
        internal IReadOnlyList<GameSession> Persisted = [];
        internal List<GameSession> Writes = [];
        internal int Loads;
        public Task<IReadOnlyList<GameSession>> GetActiveAsync(CancellationToken ct) { Loads++; return Task.FromResult(Persisted); }
        public Task<IReadOnlyList<GameSession>> GetRecentAsync(int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<GameSession>>([]);
        public Task<IReadOnlyList<GameSession>> GetByGameAsync(Guid gameId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<GameSession>>(
                Persisted
                    .Concat(Writes)
                    .Where(session => session.GameId == gameId)
                    .OrderByDescending(session => session.ObservedStartedAtUtc)
                    .ThenByDescending(session => session.SessionId)
                    .ToArray());
        public Task<GameSession?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<GameSession?>(null);
        public Task UpsertAsync(GameSession s, CancellationToken ct) { ct.ThrowIfCancellationRequested(); Writes.Add(s); return Task.CompletedTask; }
    }
    private sealed class Corrections : ISessionCorrectionStore
    {
        public Task UpsertAsync(SessionCorrection correction, CancellationToken ct) => throw new NotSupportedException();
        public Task<SessionCorrection?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<SessionCorrection?>(null);
    }
}
