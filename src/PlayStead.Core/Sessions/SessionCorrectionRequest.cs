namespace PlayStead.Core.Sessions;

public sealed record SessionCorrectionRequest(
    DateTimeOffset? CorrectedStartedAtUtc,
    DateTimeOffset? CorrectedEndedAtUtc);
