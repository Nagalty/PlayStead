using PlayStead.Core.Sessions;

namespace PlayStead.Core.Tests.Sessions;

public sealed class GameSessionTests
{
    [Fact]
    public void Session_preserves_observed_and_lifecycle_fields()
    {
        var sessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var gameId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var startedAt = new DateTimeOffset(2026, 9, 12, 20, 0, 0, TimeSpan.Zero);
        var lastSeenAt = startedAt.AddMinutes(10);
        var endedAt = startedAt.AddMinutes(10);
        var createdAt = startedAt.AddSeconds(2);
        var updatedAt = endedAt.AddSeconds(1);

        var session = new GameSession(
            sessionId,
            gameId,
            startedAt,
            lastSeenAt,
            endedAt,
            SessionState.Ended,
            SessionEndReason.ProcessExited,
            SessionDetectionSource.ProcessMonitor,
            createdAt,
            updatedAt);

        Assert.Equal(sessionId, session.SessionId);
        Assert.Equal(gameId, session.GameId);
        Assert.Equal(startedAt, session.ObservedStartedAtUtc);
        Assert.Equal(lastSeenAt, session.LastSeenAtUtc);
        Assert.Equal(endedAt, session.ObservedEndedAtUtc);
        Assert.Equal(SessionState.Ended, session.State);
        Assert.Equal(SessionEndReason.ProcessExited, session.EndReason);
        Assert.Equal(SessionDetectionSource.ProcessMonitor, session.DetectionSource);
        Assert.Equal(createdAt, session.CreatedAtUtc);
        Assert.Equal(updatedAt, session.UpdatedAtUtc);
    }
}
