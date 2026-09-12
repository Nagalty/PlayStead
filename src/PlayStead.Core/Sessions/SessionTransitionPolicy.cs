namespace PlayStead.Core.Sessions;

public sealed class SessionTransitionPolicy
{
    public SessionTransition Start(
        Guid gameId,
        DateTimeOffset firstObservedAtUtc,
        DateTimeOffset nowUtc)
    {
        var session = new GameSession(
            Guid.NewGuid(),
            gameId,
            firstObservedAtUtc,
            firstObservedAtUtc,
            null,
            SessionState.Active,
            null,
            SessionDetectionSource.ProcessMonitor,
            nowUtc,
            nowUtc);

        return new SessionTransition(
            SessionTransitionKind.Start,
            session);
    }

    public SessionTransition Heartbeat(
        GameSession active,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(active);

        if (active.State != SessionState.Active)
        {
            throw new InvalidOperationException(
                "Only an active session can receive a heartbeat.");
        }

        if (nowUtc <= active.LastSeenAtUtc)
        {
            return new SessionTransition(
                SessionTransitionKind.None,
                active);
        }

        var updated = active with
        {
            LastSeenAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };

        return new SessionTransition(
            SessionTransitionKind.Heartbeat,
            updated);
    }

    public SessionTransition End(
        GameSession active,
        DateTimeOffset lastReliableSeenAtUtc,
        SessionEndReason reason,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(active);

        if (active.State != SessionState.Active)
        {
            throw new InvalidOperationException(
                "Only an active session can be ended.");
        }

        var state = reason == SessionEndReason.RecoveredAfterUnexpectedShutdown
            ? SessionState.Recovered
            : SessionState.Ended;

        var ended = active with
        {
            LastSeenAtUtc = lastReliableSeenAtUtc,
            ObservedEndedAtUtc = lastReliableSeenAtUtc,
            State = state,
            EndReason = reason,
            UpdatedAtUtc = nowUtc
        };

        return new SessionTransition(
            SessionTransitionKind.End,
            ended);
    }
}
