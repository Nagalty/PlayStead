using PlayStead.Core.Steam;

namespace PlayStead.Providers.Steam.Remote;

public interface ISteamCmdAppInfoParser
{
    SteamRemoteEvidenceResult Parse(
        string appId,
        string branchName,
        string rawOutput,
        DateTimeOffset observedAtUtc);
}
