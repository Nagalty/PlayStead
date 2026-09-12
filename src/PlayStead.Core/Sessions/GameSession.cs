namespace PlayStead.Core.Sessions;

public sealed record GameSession(
    Guid SessionId,
    Guid GameId,
    DateTimeOffset ObservedStartedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset? ObservedEndedAtUtc,
    SessionState State,
    SessionEndReason? EndReason,
    SessionDetectionSource DetectionSource,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
