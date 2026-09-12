using PlayStead.Core.Steam;

namespace PlayStead.UI.Steam;

public sealed record SteamReferenceEntry(
    string AppId,
    string? BranchName,
    SteamUpdateEvaluation Evaluation);
