namespace PlayStead.Core.Sessions;

public sealed record SessionCorrection(
    Guid CorrectionId,
    Guid SessionId,
    DateTimeOffset? CorrectedStartedAtUtc,
    DateTimeOffset? CorrectedEndedAtUtc,
    string? Reason,
    DateTimeOffset CreatedAtUtc)
{
    public DateTimeOffset CorrectedAtUtc =>
        CreatedAtUtc;

    public SessionCorrection(
        Guid SessionId,
        DateTimeOffset? CorrectedStartedAtUtc,
        DateTimeOffset? CorrectedEndedAtUtc,
        DateTimeOffset CorrectedAtUtc)
        : this(
            Guid.NewGuid(),
            SessionId,
            CorrectedStartedAtUtc,
            CorrectedEndedAtUtc,
            Reason: null,
            CreatedAtUtc: CorrectedAtUtc)
    {
    }
}
