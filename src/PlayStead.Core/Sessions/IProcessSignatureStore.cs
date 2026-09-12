namespace PlayStead.Core.Sessions;

public interface IProcessSignatureStore
{
    Task UpsertAsync(
        ProcessSignature signature,
        CancellationToken cancellationToken);

    Task<ProcessSignature?> GetAsync(
        Guid gameId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcessSignature>> GetAllAsync(
        CancellationToken cancellationToken);
}
