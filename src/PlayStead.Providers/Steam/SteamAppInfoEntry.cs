namespace PlayStead.Providers.Steam;

public sealed record SteamMediaAssetReference(
    string Hash,
    string FileName);

public sealed record SteamMediaAssetMetadata(
    string? LibraryCapsuleHash,
    string? Library600x900Hash,
    IReadOnlyList<SteamMediaAssetReference>? CoverAssets = null)
{
    public string? LibraryAssetHash =>
        Library600x900Hash ?? LibraryCapsuleHash;
}

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
    IReadOnlyList<SteamLaunchConfiguration>? LaunchConfigurations = null,
    SteamMediaAssetMetadata? MediaAssets = null);
