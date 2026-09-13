namespace PlayStead.Core.Sessions;

public sealed record SessionCorrectionRequest(
    Guid SessionId,
    DateTimeOffset? CorrectedStartedAtUtc,
    DateTimeOffset? CorrectedEndedAtUtc,
    string? Reason)
{
    public SessionCorrectionRequest(
        DateTimeOffset? CorrectedStartedAtUtc,
        DateTimeOffset? CorrectedEndedAtUtc)
        : this(
            Guid.Empty,
            CorrectedStartedAtUtc,
            CorrectedEndedAtUtc,
            Reason: null)
    {
    }
}
