namespace PlayStead.Core.Sessions;

public enum SessionEndReason
{
    ProcessExited,
    RecoveredAfterUnexpectedShutdown,
    ManualStop,
    Corrected
}
