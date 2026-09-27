namespace PlayStead.Providers.Steam;

public sealed record SteamAppInfoEntry(
    uint AppId,
    string? Developer,
    string? Publisher,
    string? Type = null,
    IReadOnlyList<SteamLaunchConfiguration>? LaunchConfigurations = null);
