namespace PlayStead.Providers.Steam;

public sealed record SteamLaunchConfiguration(
    string Executable,
    string? WorkingDirectory,
    string? OsList);
