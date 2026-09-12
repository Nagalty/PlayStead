namespace PlayStead.Core.Steam;

public sealed record SteamRemoteEvidenceResult(
    SteamRemoteEvidenceStatus Status,
    SteamRemoteEvidence? Evidence,
    SteamRemoteFailureKind? FailureKind);
