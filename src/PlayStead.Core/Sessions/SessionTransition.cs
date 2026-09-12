namespace PlayStead.Core.Sessions;

public enum SessionTransitionKind
{
    None,
    Start,
    Heartbeat,
    End
}

public sealed record SessionTransition(
    SessionTransitionKind Kind,
    GameSession? Session);
