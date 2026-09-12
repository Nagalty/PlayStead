using PlayStead.Core.Steam;

namespace PlayStead.Providers.Steam.Remote;

public interface ISteamRemoteEvidenceProvider
{
    Task<SteamRemoteEvidenceResult> QueryAsync(
        string appId,
        string branchName,
        CancellationToken cancellationToken);
}
