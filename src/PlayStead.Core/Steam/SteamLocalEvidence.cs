namespace PlayStead.Core.Steam;

public sealed record SteamLocalEvidence(
    string AppId,
    string? BuildId,
    string? BranchName,
    IReadOnlyDictionary<string, string> DepotManifestIds,
    DateTimeOffset ObservedAtUtc);
