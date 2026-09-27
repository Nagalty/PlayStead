namespace PlayStead.Core.ProviderInstallUpdate;

public sealed record ProviderInstallUpdateEvidence(
    string? InstalledBuildId,
    string? TargetBuildId,
    string? BytesToDownload,
    string? BytesDownloaded,
    string? BytesToStage,
    string? BytesStaged,
    string? StagingSize,
    int? StateFlags,
    string? PublicBuildId = null,
    IReadOnlyDictionary<string, string>? InstalledDepotManifests = null,
    IReadOnlyDictionary<string, string>? PublicDepotManifests = null);
