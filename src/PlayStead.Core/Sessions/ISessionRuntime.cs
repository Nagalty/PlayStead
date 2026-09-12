namespace PlayStead.Core.Sessions;

public interface ISessionRuntime
{
    Task<SessionRuntimeSnapshot> RefreshAsync(
        CancellationToken cancellationToken);
}
