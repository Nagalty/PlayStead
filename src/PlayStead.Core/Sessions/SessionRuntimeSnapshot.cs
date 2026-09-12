namespace PlayStead.Core.Sessions;

public sealed record SessionRuntimeSnapshot(
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<GameSession> ActiveSessions);
