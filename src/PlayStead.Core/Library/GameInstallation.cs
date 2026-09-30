namespace PlayStead.Core.Library;

public sealed record GameInstallation(
    InstallationId Id,
    GameId GameId,
    ProviderKind Provider,
    string ExternalId,
    string InstallPath,
    long? InstalledSizeBytes,
    bool IsPreferred,
    bool IsPresent,
    DateTimeOffset LastSeenUtc,
    InstallationContentKind ContentKind = InstallationContentKind.Unknown,
    string? ExecutablePath = null,
    string? WorkingDirectory = null,
    string? LaunchArguments = null,
    string? InstallRootPath = null);
