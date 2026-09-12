namespace PlayStead.Core.Sessions;

public sealed record ProcessSignature(
    Guid GameId,
    IReadOnlyList<ProcessSignatureEntry> Entries,
    ProcessSignatureOrigin Origin,
    DateTimeOffset UpdatedAtUtc);
