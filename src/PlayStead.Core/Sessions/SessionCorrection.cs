namespace PlayStead.Core.Sessions;

public sealed record SessionCorrection(
    Guid SessionId,
    DateTimeOffset? CorrectedStartedAtUtc,
    DateTimeOffset? CorrectedEndedAtUtc,
    DateTimeOffset CorrectedAtUtc);
