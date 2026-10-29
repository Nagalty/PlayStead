using PlayStead.Core.Library;

namespace PlayStead.Core.LocalEnvironment;

public enum LocalEnvironmentComparisonState
{
    Unchanged,
    New,
    Missing,
    Changed
}

public sealed record LocalEnvironmentBaselineEntry(
    LocalEnvironmentEntryKind Kind,
    string NormalizedPath,
    LocalEnvironmentPathKind PathKind,
    bool Exists,
    DateTimeOffset? LastModifiedUtc,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record LocalEnvironmentBaseline(
    InstallationId InstallationId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<LocalEnvironmentBaselineEntry> Entries);

public sealed record LocalEnvironmentComparison(
    string EntryKey,
    LocalEnvironmentComparisonState State,
    LocalEnvironmentBaselineEntry? BaselineEntry,
    LocalEnvironmentBaselineEntry? CurrentEntry);

public interface ILocalEnvironmentBaselineStore
{
    Task<LocalEnvironmentBaseline?> GetAsync(InstallationId installationId, CancellationToken cancellationToken);
    Task ReplaceAsync(LocalEnvironmentBaseline baseline, CancellationToken cancellationToken);
}

public interface ILocalEnvironmentBaselineService
{
    Task<LocalEnvironmentBaseline> SetBaselineAsync(InstallationId installationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocalEnvironmentComparison>> CompareWithBaselineAsync(InstallationId installationId, CancellationToken cancellationToken);
}
