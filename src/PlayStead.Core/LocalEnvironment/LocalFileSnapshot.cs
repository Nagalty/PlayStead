using PlayStead.Core.Library;

namespace PlayStead.Core.LocalEnvironment;

public enum LocalFileSnapshotReason
{
    Manual = 0,
    PreRestore = 1,
    BeforeUpdate = 2
}

public sealed record LocalFileSnapshot(
    Guid Id,
    InstallationId InstallationId,
    Guid EnvironmentEntryId,
    string OriginalPath,
    DateTimeOffset CreatedAtUtc,
    LocalFileSnapshotReason Reason,
    DateTimeOffset? SourceLastModifiedUtc,
    long SourceSizeBytes,
    string Sha256,
    string SnapshotRelativePath,
    bool IsPreRestoreBackup,
    IReadOnlyDictionary<string, string>? Metadata = null);

public enum SnapshotEligibilityFailure
{
    None,
    WrongKind,
    NotAFile,
    Missing,
    UnsupportedExtension,
    TooLarge,
    UnsafePath
}

public sealed record SnapshotEligibilityResult(bool IsEligible, SnapshotEligibilityFailure Failure, long FileSizeBytes);

public static class SnapshotEligibilityPolicy
{
    public const long MaxSnapshotFileSizeBytes = 5L * 1024 * 1024;
    public const long DefaultSnapshotQuotaBytes = 256L * 1024 * 1024;
    public const int DefaultRetentionPerFile = 5;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".ini", ".json", ".xml", ".cfg", ".conf", ".toml" };

    public static SnapshotEligibilityResult Evaluate(LocalEnvironmentEntry entry)
    {
        if (entry.Kind != LocalEnvironmentEntryKind.Config) return new(false, SnapshotEligibilityFailure.WrongKind, 0);
        if (entry.PathKind != LocalEnvironmentPathKind.File) return new(false, SnapshotEligibilityFailure.NotAFile, 0);
        if (!entry.Exists || !File.Exists(entry.Path)) return new(false, SnapshotEligibilityFailure.Missing, 0);
        var fullPath = Path.GetFullPath(entry.Path);
        if (!string.Equals(fullPath, entry.Path, StringComparison.OrdinalIgnoreCase) && !Path.IsPathRooted(entry.Path)) return new(false, SnapshotEligibilityFailure.UnsafePath, 0);
        if (!AllowedExtensions.Contains(Path.GetExtension(fullPath))) return new(false, SnapshotEligibilityFailure.UnsupportedExtension, 0);
        var length = new FileInfo(fullPath).Length;
        return length <= MaxSnapshotFileSizeBytes
            ? new(true, SnapshotEligibilityFailure.None, length)
            : new(false, SnapshotEligibilityFailure.TooLarge, length);
    }
}

public interface ILocalFileSnapshotStore
{
    Task<LocalFileSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocalFileSnapshot>> GetAsync(InstallationId installationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocalFileSnapshot>> GetForEntryAsync(Guid environmentEntryId, CancellationToken cancellationToken);
    Task AddAsync(LocalFileSnapshot snapshot, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface ILocalEnvironmentSnapshotService
{
    Task<LocalFileSnapshot> CreateSnapshotAsync(InstallationId installationId, Guid environmentEntryId, LocalFileSnapshotReason reason, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocalFileSnapshot>> GetSnapshotsAsync(InstallationId installationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocalFileSnapshot>> GetSnapshotsForEntryAsync(Guid environmentEntryId, CancellationToken cancellationToken);
    Task DeleteSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);
    Task<LocalFileSnapshot> RestoreSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken);
    Task<string?> GetSnapshotPathAsync(Guid snapshotId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}
