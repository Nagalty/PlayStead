namespace PlayStead.Providers.Steam.Remote;

public sealed record SteamCmdProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration);
