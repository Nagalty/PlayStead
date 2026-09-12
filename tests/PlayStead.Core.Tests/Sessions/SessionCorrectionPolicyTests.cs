using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.Sessions;

public sealed class SessionCorrectionPolicyTests
{
    private static readonly Guid SessionId =
        Guid.Parse("88888888-1111-4444-8888-888888888888");

    private static readonly Guid GameId =
        Guid.Parse("88888888-2222-4444-8888-888888888888");

    private static readonly DateTimeOffset T0 =
        new(2026, 9, 13, 1, 5, 0, TimeSpan.Zero);

    private readonly SessionCorrectionPolicy _sut = new();

    [Fact]
    public void Create_stores_manual_overrides_without_mutating_observed_session_times()
    {
        var session = EndedSession();

        var request = new SessionCorrectionRequest(
            CorrectedStartedAtUtc:
                T0.AddMinutes(-5),
            CorrectedEndedAtUtc:
                T0.AddMinutes(65));

        var correction = _sut.Create(
            session,
            request,
            T0.AddMinutes(70));

        Assert.Equal(
            SessionId,
            correction.SessionId);

        Assert.Equal(
            T0.AddMinutes(-5),
            correction.CorrectedStartedAtUtc);

        Assert.Equal(
            T0.AddMinutes(65),
            correction.CorrectedEndedAtUtc);

        Assert.Equal(
            T0.AddMinutes(70),
            correction.CorrectedAtUtc);

        Assert.Equal(
            T0,
            session.ObservedStartedAtUtc);

        Assert.Equal(
            T0.AddMinutes(60),
            session.ObservedEndedAtUtc);
    }

    [Fact]
    public void Resolve_uses_manual_override_and_observed_fallback_for_effective_time()
    {
        var session = EndedSession();

        var correction = new SessionCorrection(
            SessionId,
            CorrectedStartedAtUtc:
                T0.AddMinutes(-3),
            CorrectedEndedAtUtc:
                null,
            CorrectedAtUtc:
                T0.AddMinutes(70));

        var effective = _sut.Resolve(
            session,
            correction);

        Assert.Equal(
            T0.AddMinutes(-3),
            effective.StartedAtUtc);

        Assert.Equal(
            T0.AddMinutes(60),
            effective.EndedAtUtc);

        Assert.True(
            effective.IsManuallyCorrected);
    }

    [Fact]
    public void Resolve_without_correction_returns_observed_time()
    {
        var session = EndedSession();

        var effective = _sut.Resolve(
            session,
            correction: null);

        Assert.Equal(
            session.ObservedStartedAtUtc,
            effective.StartedAtUtc);

        Assert.Equal(
            session.ObservedEndedAtUtc,
            effective.EndedAtUtc);

        Assert.False(
            effective.IsManuallyCorrected);
    }

    [Fact]
    public void Create_rejects_request_without_any_manual_override()
    {
        var session = EndedSession();

        var request = new SessionCorrectionRequest(
            CorrectedStartedAtUtc: null,
            CorrectedEndedAtUtc: null);

        Assert.Throws<ArgumentException>(
            () => _sut.Create(
                session,
                request,
                T0.AddMinutes(70)));
    }

    [Fact]
    public void Create_rejects_effective_end_that_is_not_after_effective_start()
    {
        var session = EndedSession();

        var request = new SessionCorrectionRequest(
            CorrectedStartedAtUtc:
                T0.AddMinutes(61),
            CorrectedEndedAtUtc:
                null);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => _sut.Create(
                session,
                request,
                T0.AddMinutes(70)));
    }

    [Fact]
    public void Create_rejects_correction_of_active_session()
    {
        var active = EndedSession() with
        {
            LastSeenAtUtc = T0.AddMinutes(10),
            ObservedEndedAtUtc = null,
            State = SessionState.Active,
            EndReason = null
        };

        var request = new SessionCorrectionRequest(
            CorrectedStartedAtUtc:
                T0.AddMinutes(-1),
            CorrectedEndedAtUtc:
                null);

        Assert.Throws<InvalidOperationException>(
            () => _sut.Create(
                active,
                request,
                T0.AddMinutes(70)));
    }

    private static GameSession EndedSession()
        => new(
            SessionId,
            GameId,
            T0,
            T0.AddMinutes(60),
            T0.AddMinutes(60),
            SessionState.Ended,
            SessionEndReason.ProcessExited,
            SessionDetectionSource.ProcessMonitor,
            T0,
            T0.AddMinutes(60));
}
