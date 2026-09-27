using PlayStead.Core.Library;

namespace PlayStead.Core.ProviderInstallUpdate;

public sealed record ProviderInstallUpdateState(
    GameId GameId,
    ProviderKind Provider,
    string ProviderGameId,
    string? InstalledBuildId,
    string? TargetBuildId,
    ProviderInstallUpdateStatus Status,
    long? BytesToDownload,
    long? BytesDownloaded,
    long? BytesToStage,
    long? BytesStaged,
    long? StagingSize,
    int? StateFlags,
    DateTimeOffset ObservedAtUtc);
