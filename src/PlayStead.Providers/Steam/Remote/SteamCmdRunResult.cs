namespace PlayStead.Providers.Steam.Remote;

public sealed record SteamCmdRunResult(
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    TimeSpan Duration);
