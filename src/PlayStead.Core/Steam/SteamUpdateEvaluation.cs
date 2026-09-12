namespace PlayStead.Core.Steam;

public sealed record SteamUpdateEvaluation(
    SteamUpdateState State,
    SteamUpdateReason Reason,
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyList<string> ChangedDepotIds);
