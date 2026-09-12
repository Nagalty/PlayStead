namespace PlayStead.Core.Sessions;

public sealed record EffectiveSessionTime(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    bool IsManuallyCorrected);
