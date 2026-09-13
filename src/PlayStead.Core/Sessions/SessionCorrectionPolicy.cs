namespace PlayStead.Core.Sessions;

public sealed class SessionCorrectionPolicy
{
    public SessionCorrection Create(
        GameSession session,
        SessionCorrectionRequest request,
        DateTimeOffset correctedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        if (request.SessionId != Guid.Empty &&
            request.SessionId != session.SessionId)
        {
            throw new ArgumentException(
                "Session correction request does not belong to the supplied session.",
                nameof(request));
        }

        if (session.State == SessionState.Active)
        {
            throw new InvalidOperationException(
                "An active session cannot be manually corrected.");
        }

        if (request.CorrectedStartedAtUtc is null &&
            request.CorrectedEndedAtUtc is null)
        {
            throw new ArgumentException(
                "At least one manual session time override is required.",
                nameof(request));
        }

        var effectiveStart =
            request.CorrectedStartedAtUtc
            ?? session.ObservedStartedAtUtc;

        var effectiveEnd =
            request.CorrectedEndedAtUtc
            ?? session.ObservedEndedAtUtc;

        if (effectiveEnd is null)
        {
            throw new InvalidOperationException(
                "A completed session must have an observed or corrected end time.");
        }

        if (effectiveEnd <= effectiveStart)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Effective session end time must be after its start time.");
        }

        return new SessionCorrection(
            Guid.NewGuid(),
            session.SessionId,
            request.CorrectedStartedAtUtc,
            request.CorrectedEndedAtUtc,
            request.Reason,
            correctedAtUtc);
    }

    public EffectiveSessionTime Resolve(
        GameSession session,
        SessionCorrection? correction)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (correction is not null &&
            correction.SessionId != session.SessionId)
        {
            throw new ArgumentException(
                "Session correction does not belong to the supplied session.",
                nameof(correction));
        }

        return new EffectiveSessionTime(
            correction?.CorrectedStartedAtUtc
                ?? session.ObservedStartedAtUtc,
            correction?.CorrectedEndedAtUtc
                ?? session.ObservedEndedAtUtc,
            correction is not null);
    }
}
