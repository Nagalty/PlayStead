namespace PlayStead.Core.LocalArtifacts;

public sealed record LocalSnapshotStorageUsage(
    long UsedBytes,
    long QuotaBytes,
    int SnapshotCount,
    DateTimeOffset? OldestSnapshotUtc);

public sealed class SnapshotStorageQuotaExceededException(long quotaBytes, long requiredBytes)
    : IOException($"Snapshot storage quota exceeded. Quota={quotaBytes} Required={requiredBytes}.")
{
    public long QuotaBytes { get; } = quotaBytes;
    public long RequiredBytes { get; } = requiredBytes;
}

public interface ILocalSnapshotStorageService
{
    long QuotaBytes { get; }
    void SetQuotaBytes(long quotaBytes);
    Task<LocalSnapshotStorageUsage> GetUsageAsync(CancellationToken cancellationToken);
    Task ApplyRetentionAsync(CancellationToken cancellationToken);
    Task EnsureCapacityForAsync(LocalArtifactSnapshot candidate, CancellationToken cancellationToken);
    Task DeleteSnapshotAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken);
}

public sealed class LocalSnapshotStorageService(
    ILocalArtifactSnapshotStore store,
    long quotaBytes = 1_073_741_824) : ILocalSnapshotStorageService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _quotaBytes = quotaBytes;

    public long QuotaBytes => Interlocked.Read(ref _quotaBytes);

    public void SetQuotaBytes(long quotaBytes)
    {
        if (quotaBytes <= 0) throw new ArgumentOutOfRangeException(nameof(quotaBytes));
        Interlocked.Exchange(ref _quotaBytes, quotaBytes);
    }

    public async Task<LocalSnapshotStorageUsage> GetUsageAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await GetUsageCoreAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task ApplyRetentionAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshots = await store.GetAllAsync(cancellationToken).ConfigureAwait(false);
            await ApplyRetentionCoreAsync(snapshots, 0, null, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task EnsureCapacityForAsync(LocalArtifactSnapshot candidate, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var candidateBytes = GetLength(candidate.ArchivePath);
            var snapshots = await store.GetAllAsync(cancellationToken).ConfigureAwait(false);
            await ApplyRetentionCoreAsync(snapshots, candidateBytes, candidate.SnapshotId, cancellationToken).ConfigureAwait(false);
            var usage = await GetUsageCoreAsync(cancellationToken).ConfigureAwait(false);
            var required = checked(usage.UsedBytes + candidateBytes);
            if (required > QuotaBytes)
                throw new SnapshotStorageQuotaExceededException(QuotaBytes, required);
        }
        catch
        {
            if (File.Exists(candidate.ArchivePath)) File.Delete(candidate.ArchivePath);
            throw;
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteSnapshotAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(snapshot.ArchivePath)) File.Delete(snapshot.ArchivePath);
            await store.DeleteAsync(snapshot.SnapshotId, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task ApplyRetentionCoreAsync(
        IReadOnlyList<LocalArtifactSnapshot> snapshots,
        long additionalBytes,
        Guid? protectedSnapshotId,
        CancellationToken cancellationToken)
    {
        var usage = await GetUsageCoreAsync(cancellationToken).ConfigureAwait(false);
        var required = checked(usage.UsedBytes + additionalBytes);
        if (required <= QuotaBytes) return;

        var preserveIds = snapshots
            .Where(x => x.IsValid && File.Exists(x.ArchivePath))
            .GroupBy(x => (x.GameId, x.ArtifactKind, x.RuleIdentity))
            .Select(g => g.OrderByDescending(x => x.CreatedAtUtc).First().SnapshotId)
            .ToHashSet();

        foreach (var snapshot in snapshots.OrderBy(x => x.CreatedAtUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (protectedSnapshotId == snapshot.SnapshotId || preserveIds.Contains(snapshot.SnapshotId) || snapshot.Reason == SnapshotReason.PreRestore)
                continue;
            if (!File.Exists(snapshot.ArchivePath)) continue;
            await DeleteSnapshotCoreAsync(snapshot, cancellationToken).ConfigureAwait(false);
            var current = await GetUsageCoreAsync(cancellationToken).ConfigureAwait(false);
            if (current.UsedBytes + additionalBytes <= QuotaBytes) return;
        }
    }

    private async Task DeleteSnapshotCoreAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (File.Exists(snapshot.ArchivePath)) File.Delete(snapshot.ArchivePath);
        await store.DeleteAsync(snapshot.SnapshotId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LocalSnapshotStorageUsage> GetUsageCoreAsync(CancellationToken cancellationToken)
    {
        var snapshots = await store.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var files = snapshots
            .Where(x => File.Exists(x.ArchivePath))
            .Select(x => (Snapshot: x, Length: GetLength(x.ArchivePath)))
            .ToArray();
        return new LocalSnapshotStorageUsage(
            files.Sum(x => x.Length),
            QuotaBytes,
            files.Length,
            files.OrderBy(x => x.Snapshot.CreatedAtUtc).Select(x => (DateTimeOffset?)x.Snapshot.CreatedAtUtc).FirstOrDefault());
    }

    private static long GetLength(string path) => new FileInfo(path).Length;
}
