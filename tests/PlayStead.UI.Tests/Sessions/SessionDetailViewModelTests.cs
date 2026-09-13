using System.ComponentModel;
using System.Reflection;
using PlayStead.Core.Sessions;

namespace PlayStead.UI.Tests.Sessions;

public sealed class SessionDetailViewModelTests
{

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B8_Detail_title_comes_from_the_supplied_game()
    {
        var sut = CreateDetail();
        Assert.Equal("Jeu de test", sut.Title);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B8_Observed_timestamps_are_not_rewritten_by_correction()
    {
        var session = DetailSession();
        var sut = CreateDetail(session, DetailCorrection(session, Utc(10, 15), Utc(11, 45)));

        Assert.Equal(Label(Utc(10)), sut.ObservedStartedAtLabel);
        Assert.Equal(Label(Utc(11)), sut.ObservedEndedAtLabel);
        Assert.Equal(Utc(10), session.ObservedStartedAtUtc);
        Assert.Equal(Utc(11), session.ObservedEndedAtUtc);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B8_Observed_duration_stays_one_hour_after_correction()
    {
        var session = DetailSession();
        var sut = CreateDetail(session, DetailCorrection(session, Utc(10, 15), Utc(11, 45)));
        Assert.Equal("1:00:00", sut.ObservedDurationLabel);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B8_Effective_timestamps_use_the_correction()
    {
        var session = DetailSession();
        var sut = CreateDetail(session, DetailCorrection(session, Utc(10, 15), Utc(11, 45)));

        Assert.Equal(Label(Utc(10, 15)), sut.EffectiveStartedAtLabel);
        Assert.Equal(Label(Utc(11, 45)), sut.EffectiveEndedAtLabel);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B8_Effective_corrected_duration_is_ninety_minutes()
    {
        var session = DetailSession();
        var sut = CreateDetail(session, DetailCorrection(session, Utc(10, 15), Utc(11, 45)));
        Assert.Equal("1:30:00", sut.EffectiveDurationLabel);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B8_Without_correction_effective_projection_matches_observed_values()
    {
        var sut = CreateDetail();

        // Literal fixture expectations prevent two empty projections from satisfying the test.
        Assert.Equal(Label(Utc(10)), sut.EffectiveStartedAtLabel);
        Assert.Equal(Label(Utc(11)), sut.EffectiveEndedAtLabel);
        Assert.Equal("1:00:00", sut.EffectiveDurationLabel);
        Assert.Equal(sut.ObservedStartedAtLabel, sut.EffectiveStartedAtLabel);
        Assert.Equal(sut.ObservedEndedAtLabel, sut.EffectiveEndedAtLabel);
        Assert.Equal(sut.ObservedDurationLabel, sut.EffectiveDurationLabel);
        Assert.False(sut.IsCorrected);
    }

    [Theory]
    [InlineData(true, "45:00")]
    [InlineData(false, "1:45:00")]
    [Trait("Task11Cycle", "B")]
    public void B9_Partial_correction_falls_back_to_the_other_observed_timestamp(bool startOnly, string duration)
    {
        var session = DetailSession();
        var correction = DetailCorrection(session,
            startOnly ? Utc(10, 15) : null, startOnly ? null : Utc(11, 45));
        var sut = CreateDetail(session, correction);

        Assert.Equal(Label(startOnly ? Utc(10, 15) : Utc(10)), sut.EffectiveStartedAtLabel);
        Assert.Equal(Label(startOnly ? Utc(11) : Utc(11, 45)), sut.EffectiveEndedAtLabel);
        Assert.Equal(duration, sut.EffectiveDurationLabel);
        Assert.True(sut.IsCorrected);
    }

    [Fact]
    [Trait("Task11Cycle", "B")]
    public void B10_Correction_reason_does_not_replace_the_observed_end_reason()
    {
        var session = DetailSession();
        var corrected = CreateDetail(session, DetailCorrection(session, Utc(10, 15), Utc(11, 45)));
        var observed = CreateDetail(session);
        var manuallyStopped = CreateDetail(session with { EndReason = SessionEndReason.ManualStop });

        Assert.False(string.IsNullOrWhiteSpace(corrected.EndReasonLabel));
        Assert.Equal(observed.EndReasonLabel, corrected.EndReasonLabel);
        Assert.NotEqual("Correction manuelle", corrected.EndReasonLabel);
        Assert.NotEqual(manuallyStopped.EndReasonLabel, corrected.EndReasonLabel);
        Assert.Equal(SessionEndReason.ProcessExited, session.EndReason);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("Task11Cycle", "B")]
    public void B11_Recovery_and_correction_are_independent_flags(bool recovered, bool corrected)
    {
        var session = DetailSession();
        if (recovered)
            session = session with { State = SessionState.Recovered, EndReason = SessionEndReason.RecoveredAfterUnexpectedShutdown };
        var sut = CreateDetail(session, corrected ? DetailCorrection(session, Utc(10, 15), Utc(11, 45)) : null);

        Assert.Equal(recovered, sut.IsRecovered);
        Assert.Equal(corrected, sut.IsCorrected);
    }

    private static DateTimeOffset Utc(int hour, int minute = 0) =>
        new(2026, 9, 12, hour, minute, 0, TimeSpan.Zero);

    // Match the existing SessionViewModel display conventions: local "g", h:mm:ss / mm:ss.
    private static string Label(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);

    private static GameSession DetailSession() =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Utc(10), Utc(11), Utc(11), SessionState.Ended, SessionEndReason.ProcessExited,
            SessionDetectionSource.ProcessMonitor, Utc(10), Utc(11));

    private static SessionCorrection DetailCorrection(GameSession session, DateTimeOffset? start, DateTimeOffset? end) =>
        new SessionCorrectionPolicy().Create(session,
            new SessionCorrectionRequest(session.SessionId, start, end, "Correction manuelle"), Utc(12));

    private static PlayStead.UI.Sessions.SessionDetailViewModel CreateDetail(
        GameSession? session = null, SessionCorrection? correction = null)
    {
        session ??= DetailSession();
        var policy = new SessionCorrectionPolicy();
        var editor = new PlayStead.UI.Sessions.SessionCorrectionViewModel(
            session, correction, new UnusedRuntime(), policy, new FixedTimeProvider(), _ => Task.CompletedTask);
        return new PlayStead.UI.Sessions.SessionDetailViewModel("Jeu de test", session, correction, policy, editor);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Utc(12);
    }

    private sealed class UnusedRuntime : ISessionRuntime
    {
        public Task CorrectSessionAsync(SessionCorrectionRequest correction, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Detail projection must not save a correction.");

        public Task<SessionRuntimeSnapshot> RefreshAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Detail projection must not start the runtime.");
    }
    [Fact]
    [Trait("Task11Cycle", "A")]
    public void Detail_contract_has_read_only_observed_effective_status_and_correction_properties()
    {
        var type = SessionCycleAContract.RequireType("SessionDetailViewModel");
        Assert.True(typeof(INotifyPropertyChanged).IsAssignableFrom(type));

        foreach (var name in new[]
        {
            "Title",
            "ObservedStartedAtLabel",
            "ObservedEndedAtLabel",
            "ObservedDurationLabel",
            "EffectiveStartedAtLabel",
            "EffectiveEndedAtLabel",
            "EffectiveDurationLabel",
            "EndReasonLabel"
        })
        {
            SessionCycleAContract.AssertProperty(type, name, typeof(string));
        }

        SessionCycleAContract.AssertProperty(type, "IsRecovered", typeof(bool));
        SessionCycleAContract.AssertProperty(type, "IsCorrected", typeof(bool));
        var correctionType = SessionCycleAContract.RequireType("SessionCorrectionViewModel");
        SessionCycleAContract.AssertProperty(type, "Correction", correctionType);
    }

    [Fact]
    [Trait("Task11Cycle", "A")]
    public void Detail_contract_is_constructible_from_session_correction_policy_and_editor()
    {
        var type = SessionCycleAContract.RequireType("SessionDetailViewModel");
        var correctionType = SessionCycleAContract.RequireType("SessionCorrectionViewModel");
        var constructor = type.GetConstructor(
            [typeof(string), typeof(GameSession), typeof(SessionCorrection),
                typeof(SessionCorrectionPolicy), correctionType]);

        Assert.True(constructor is not null,
            "Expected public constructor (string, GameSession, SessionCorrection?, " +
            "SessionCorrectionPolicy, SessionCorrectionViewModel).");
        Assert.Equal(
            new[] { typeof(string), typeof(GameSession), typeof(SessionCorrection),
                typeof(SessionCorrectionPolicy), correctionType },
            constructor!.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        Assert.Equal(NullabilityState.Nullable,
            new NullabilityInfoContext().Create(constructor.GetParameters()[2]).ReadState);

        var instance = constructor.Invoke(
            ["Jeu local", SessionCycleAContract.CreateSession(), null,
                new SessionCorrectionPolicy(), SessionCycleAContract.CreateCorrection(correctionType)]);

        Assert.IsAssignableFrom<INotifyPropertyChanged>(instance);
    }
}
