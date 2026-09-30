namespace PlayStead.UI.Launching;

public sealed record LaunchedProcessIdentity(
    int ProcessId,
    DateTimeOffset StartedAtUtc);
