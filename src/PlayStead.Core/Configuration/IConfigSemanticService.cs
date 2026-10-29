namespace PlayStead.Core.Configuration;

public interface IConfigSemanticService
{
    Task<IReadOnlyList<ConfigSemanticChange>> CompareConfigAsync(string currentPath, string referencePath, CancellationToken cancellationToken);
    Task<ConfigRestoreResult> RestoreConfigValuesAsync(string currentPath, string referencePath, IReadOnlyCollection<string> selectedChangeIds, CancellationToken cancellationToken);
}

public sealed record ConfigRestoreResult(bool Succeeded, string? BackupPath, IReadOnlyList<ConfigSemanticChange> AppliedChanges, string? FailureReason);

public interface IConfigBackupWriter
{
    Task<string> CreateBackupAsync(string currentPath, CancellationToken cancellationToken);
}

public interface IConfigAtomicWriter
{
    Task WriteAsync(string path, string content, CancellationToken cancellationToken);
}
