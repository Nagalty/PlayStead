namespace PlayStead.Providers.Steam;

public sealed record SteamAppInfoEntry(
    uint AppId,
    string? Developer,
    string? Publisher,
    IReadOnlyList<string>? Genres = null,
    IReadOnlyList<string>? Categories = null,
    string? ReleaseDateText = null,
    bool ReleaseDateReported = false,
    bool? IsFree = null,
    IReadOnlyList<string>? Developers = null,
    IReadOnlyList<string>? Publishers = null,
    string? PublicBuildId = null,
    IReadOnlyDictionary<string, string>? PublicDepotManifests = null,
    string? Type = null,
    IReadOnlyList<SteamLaunchConfiguration>? LaunchConfigurations = null);
