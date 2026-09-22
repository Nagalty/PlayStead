namespace PlayStead.Core.Sessions;

public interface IStartupSessionReconciler
{
    Task<SessionRuntimeSnapshot> ReconcileRunningProcessesAsync(
        CancellationToken cancellationToken);
}
