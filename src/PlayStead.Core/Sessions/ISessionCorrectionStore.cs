namespace PlayStead.Core.Sessions;

public interface ISessionCorrectionStore
{
    Task UpsertAsync(
        SessionCorrection correction,
        CancellationToken cancellationToken);

    Task<SessionCorrection?> GetAsync(
        Guid sessionId,
        CancellationToken cancellationToken);
}
