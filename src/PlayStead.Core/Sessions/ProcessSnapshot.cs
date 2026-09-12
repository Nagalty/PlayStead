namespace PlayStead.Core.Sessions;

public sealed record ProcessSnapshot(
    int ProcessId,
    string ExecutableName,
    string? ExecutablePath,
    DateTimeOffset? StartedAtUtc);
