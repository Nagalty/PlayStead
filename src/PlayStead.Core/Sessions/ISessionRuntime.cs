namespace PlayStead.Core.Sessions;

public interface ISessionRuntime
{
    Task<SessionRuntimeSnapshot> RefreshAsync(
        CancellationToken cancellationToken);

    Task CorrectSessionAsync(
        SessionCorrectionRequest correction,
        CancellationToken cancellationToken);
}
