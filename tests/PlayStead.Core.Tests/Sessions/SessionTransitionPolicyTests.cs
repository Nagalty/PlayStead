using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.Sessions;

public sealed class SessionTransitionPolicyTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset T0 = new(2026, 9, 12, 20, 0, 0, TimeSpan.Zero);

    private readonly SessionTransitionPolicy _sut = new();

    [Fact]
    public void Start_creates_active_session_from_first_reliable_observation()
    {
        var result = _sut.Start(
            GameId,
            T0,
            T0.AddSeconds(2));

        Assert.Equal(SessionTransitionKind.Start, result.Kind);
        Assert.NotNull(result.Session);
        Assert.Equal(GameId, result.Session.GameId);
        Assert.Equal(SessionState.Active, result.Session.State);
        Assert.Equal(T0, result.Session.ObservedStartedAtUtc);
        Assert.Equal(T0, result.Session.LastSeenAtUtc);
        Assert.Null(result.Session.ObservedEndedAtUtc);
        Assert.Null(result.Session.EndReason);
        Assert.Equal(SessionDetectionSource.ProcessMonitor, result.Session.DetectionSource);
        Assert.Equal(T0.AddSeconds(2), result.Session.CreatedAtUtc);
        Assert.Equal(T0.AddSeconds(2), result.Session.UpdatedAtUtc);
    }

    [Fact]
    public void Heartbeat_advances_last_seen_without_changing_identity_or_start()
    {
        var started = _sut.Start(
            GameId,
            T0,
            T0).Session!;

        var result = _sut.Heartbeat(
            started,
            T0.AddMinutes(5));

        Assert.Equal(SessionTransitionKind.Heartbeat, result.Kind);
        Assert.NotNull(result.Session);
        Assert.Equal(started.SessionId, result.Session.SessionId);
        Assert.Equal(started.GameId, result.Session.GameId);
        Assert.Equal(started.ObservedStartedAtUtc, result.Session.ObservedStartedAtUtc);
        Assert.Equal(T0.AddMinutes(5), result.Session.LastSeenAtUtc);
        Assert.Equal(SessionState.Active, result.Session.State);
        Assert.Null(result.Session.ObservedEndedAtUtc);
        Assert.Null(result.Session.EndReason);
    }

    [Fact]
    public void Heartbeat_never_moves_last_seen_backwards()
    {
        var started = _sut.Start(
            GameId,
            T0,
            T0).Session!;

        var current = started with
        {
            LastSeenAtUtc = T0.AddMinutes(5),
            UpdatedAtUtc = T0.AddMinutes(5)
        };

        var result = _sut.Heartbeat(
            current,
            T0.AddMinutes(3));

        Assert.Equal(SessionTransitionKind.None, result.Kind);
        Assert.Same(current, result.Session);
        Assert.Equal(T0.AddMinutes(5), result.Session!.LastSeenAtUtc);
    }

    [Fact]
    public void End_process_exit_preserves_start_and_ends_at_last_reliable_timestamp()
    {
        var started = _sut.Start(
            GameId,
            T0,
            T0).Session!;

        var result = _sut.End(
            started,
            T0.AddMinutes(12),
            SessionEndReason.ProcessExited,
            T0.AddMinutes(20));

        Assert.Equal(SessionTransitionKind.End, result.Kind);
        Assert.NotNull(result.Session);
        Assert.Equal(started.SessionId, result.Session.SessionId);
        Assert.Equal(started.ObservedStartedAtUtc, result.Session.ObservedStartedAtUtc);
        Assert.Equal(T0.AddMinutes(12), result.Session.LastSeenAtUtc);
        Assert.Equal(T0.AddMinutes(12), result.Session.ObservedEndedAtUtc);
        Assert.Equal(SessionState.Ended, result.Session.State);
        Assert.Equal(SessionEndReason.ProcessExited, result.Session.EndReason);
        Assert.Equal(T0.AddMinutes(20), result.Session.UpdatedAtUtc);
    }

    [Fact]
    public void End_recovered_session_at_last_reliable_timestamp()
    {
        var started = _sut.Start(
            GameId,
            T0,
            T0).Session!;

        var result = _sut.End(
            started,
            T0.AddMinutes(12),
            SessionEndReason.RecoveredAfterUnexpectedShutdown,
            T0.AddMinutes(20));

        Assert.Equal(SessionTransitionKind.End, result.Kind);
        Assert.NotNull(result.Session);
        Assert.Equal(SessionState.Recovered, result.Session.State);
        Assert.Equal(SessionEndReason.RecoveredAfterUnexpectedShutdown, result.Session.EndReason);
        Assert.Equal(T0.AddMinutes(12), result.Session.ObservedEndedAtUtc);
        Assert.Equal(T0.AddMinutes(12), result.Session.LastSeenAtUtc);
    }

    [Fact]
    public void End_rejects_a_session_that_is_not_active()
    {
        var started = _sut.Start(
            GameId,
            T0,
            T0).Session!;

        var ended = _sut.End(
            started,
            T0.AddMinutes(1),
            SessionEndReason.ProcessExited,
            T0.AddMinutes(1)).Session!;

        Assert.Throws<InvalidOperationException>(() =>
            _sut.End(
                ended,
                T0.AddMinutes(2),
                SessionEndReason.ProcessExited,
                T0.AddMinutes(2)));
    }
}
