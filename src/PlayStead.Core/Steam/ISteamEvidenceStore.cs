namespace PlayStead.Core.Steam;

public interface ISteamEvidenceStore
{
    Task ReplaceLocalAsync(
        IReadOnlyCollection<SteamLocalEvidence> evidence,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SteamLocalEvidence>> GetLocalAsync(
        CancellationToken cancellationToken);

    Task<SteamRemoteEvidence?> GetRemoteAsync(
        string appId,
        string branchName,
        CancellationToken cancellationToken);

    Task UpsertRemoteAsync(
        SteamRemoteEvidence evidence,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SteamRemoteEvidence>> GetAllRemoteAsync(
        CancellationToken cancellationToken);

    Task ClearRemoteAsync(
        CancellationToken cancellationToken);
}
