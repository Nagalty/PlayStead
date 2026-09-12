namespace PlayStead.Core.Steam;

public sealed record SteamRemoteEvidence(
    string AppId,
    string BranchName,
    string? BuildId,
    IReadOnlyDictionary<string, string> DepotManifestIds,
    DateTimeOffset ObservedAtUtc,
    SteamRemoteEvidenceSource Source);
