using PlayStead.Core.Library;

namespace PlayStead.Core.LocalEnvironment;

public interface ILocalEnvironmentContributor
{
    Task<IReadOnlyList<LocalEnvironmentEntry>> DiscoverAsync(
        GameInstallation installation,
        CancellationToken cancellationToken);
}

public interface ILocalEnvironmentStore
{
    Task<IReadOnlyList<LocalEnvironmentEntry>> GetEntriesAsync(
        InstallationId installationId,
        CancellationToken cancellationToken);

    Task ReplaceDetectedAsync(
        InstallationId installationId,
        IReadOnlyCollection<LocalEnvironmentEntry> entries,
        CancellationToken cancellationToken);

    Task UpsertUserOverrideAsync(
        LocalEnvironmentEntry entry,
        CancellationToken cancellationToken);

    Task RemoveUserOverrideAsync(
        InstallationId installationId,
        LocalEnvironmentEntryKind kind,
        string path,
        CancellationToken cancellationToken);
}

public interface ILocalEnvironmentDiscoveryService
{
    Task<GameLocalEnvironment> GetEnvironmentAsync(
        InstallationId installationId,
        CancellationToken cancellationToken);

    Task<GameLocalEnvironment> RefreshEnvironmentAsync(
        InstallationId installationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LocalEnvironmentEntry>> GetConfigsAsync(InstallationId installationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LocalEnvironmentEntry>> GetLogsAsync(InstallationId installationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<LocalEnvironmentEntry>> GetSavesAsync(InstallationId installationId, CancellationToken cancellationToken);

    Task<LocalEnvironmentCustomizationState> GetCustomizationStateAsync(InstallationId installationId, CancellationToken cancellationToken);

    Task RemoveUserOverrideAsync(
        InstallationId installationId,
        LocalEnvironmentEntryKind kind,
        string path,
        CancellationToken cancellationToken);
}
