namespace PlayStead.Core.Steam;

public enum SteamUpdateReason
{
    None,
    DepotManifestsMatch,
    DepotManifestMismatch,
    RemoteBuildChangedWithoutDepotDifference,
    RemoteBuildChangedWithIncompleteDepotEvidence,
    LocalBranchUnknown,
    RemoteBranchUnavailable,
    LocalDepotEvidenceMissing,
    RemoteDepotEvidenceMissing,
    RemoteRefreshFailedWithoutCache,
    EvidenceContradictory
}
