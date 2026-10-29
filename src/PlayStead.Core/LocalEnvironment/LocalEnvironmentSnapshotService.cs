using System.Collections.Concurrent;
using System.Security.Cryptography;
using PlayStead.Core.Library;

namespace PlayStead.Core.LocalEnvironment;

public sealed class LocalEnvironmentSnapshotService : ILocalEnvironmentSnapshotService
{
    private readonly ILocalEnvironmentDiscoveryService _environment;
    private readonly ILocalFileSnapshotStore _store;
    private readonly string _root;
    private readonly TimeProvider _clock;
    private readonly long _quotaBytes;
    private readonly int _retentionPerFile;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public LocalEnvironmentSnapshotService(
        ILocalEnvironmentDiscoveryService environment,
        ILocalFileSnapshotStore store,
        string snapshotsRoot,
        TimeProvider? clock = null,
        long quotaBytes = SnapshotEligibilityPolicy.DefaultSnapshotQuotaBytes,
        int retentionPerFile = SnapshotEligibilityPolicy.DefaultRetentionPerFile)
    {
        _environment = environment;
        _store = store;
        _root = Path.GetFullPath(snapshotsRoot);
        _clock = clock ?? TimeProvider.System;
        if (quotaBytes <= 0) throw new ArgumentOutOfRangeException(nameof(quotaBytes));
        if (retentionPerFile <= 0) throw new ArgumentOutOfRangeException(nameof(retentionPerFile));
        _quotaBytes = quotaBytes;
        _retentionPerFile = retentionPerFile;
    }

    public async Task<LocalFileSnapshot> CreateSnapshotAsync(InstallationId installationId, Guid environmentEntryId, LocalFileSnapshotReason reason, CancellationToken cancellationToken)
    {
        var current = await _environment.GetEnvironmentAsync(installationId, cancellationToken).ConfigureAwait(false);
        var entry = current.Entries.FirstOrDefault(x => x.Id == environmentEntryId) ?? throw new InvalidOperationException("Environment entry was not found.");
        var eligibility = SnapshotEligibilityPolicy.Evaluate(entry);
        if (!eligibility.IsEligible) throw new InvalidOperationException($"Environment entry is not snapshot eligible: {eligibility.Failure}.");
        var sourcePath = Path.GetFullPath(entry.Path);
        var gate = _locks.GetOrAdd(sourcePath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await CreateSnapshotCoreAsync(installationId, environmentEntryId, entry, eligibility, reason, sourcePath, cancellationToken).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    private async Task<LocalFileSnapshot> CreateSnapshotCoreAsync(InstallationId installationId, Guid environmentEntryId, LocalEnvironmentEntry entry, SnapshotEligibilityResult eligibility, LocalFileSnapshotReason reason, string sourcePath, CancellationToken cancellationToken)
    {
            var hash = await ComputeHashAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            var existing = await _store.GetForEntryAsync(environmentEntryId, cancellationToken).ConfigureAwait(false);
            var recent = existing.FirstOrDefault(x => !x.IsPreRestoreBackup && x.SourceSizeBytes == eligibility.FileSizeBytes && string.Equals(x.Sha256, hash, StringComparison.OrdinalIgnoreCase) && _clock.GetUtcNow() - x.CreatedAtUtc < TimeSpan.FromMinutes(1) && File.Exists(GetAbsoluteSnapshotPath(x)));
            if (recent is not null) return recent;

            var id = Guid.NewGuid();
            var safeName = SanitizeFileName(Path.GetFileName(sourcePath));
            var relative = Path.Combine(installationId.Value.ToString("D"), environmentEntryId.ToString("N"), $"{_clock.GetUtcNow():yyyyMMddHHmmssfff}_{id:N}_{safeName}");
            var absolute = GetAbsoluteSnapshotPath(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            var temp = absolute + ".tmp";
            var metadataAdded = false;
            try
            {
                await CopyAsync(sourcePath, temp, cancellationToken).ConfigureAwait(false);
                var copiedHash = await ComputeHashAsync(temp, cancellationToken).ConfigureAwait(false);
                var sourceAfterHash = await ComputeHashAsync(sourcePath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(copiedHash, hash, StringComparison.OrdinalIgnoreCase) || !string.Equals(sourceAfterHash, hash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Source changed while the snapshot was being created.");
                File.Move(temp, absolute, true);
                var snapshot = new LocalFileSnapshot(id, installationId, environmentEntryId, sourcePath, _clock.GetUtcNow(), reason, entry.LastModifiedUtc, eligibility.FileSizeBytes, hash, relative, reason == LocalFileSnapshotReason.PreRestore, entry.Metadata);
                await _store.AddAsync(snapshot, cancellationToken).ConfigureAwait(false);
                metadataAdded = true;
                await PurgeAsync(installationId, environmentEntryId, cancellationToken).ConfigureAwait(false);
                return snapshot;
            }
            catch
            {
                if (File.Exists(temp)) File.Delete(temp);
                if (metadataAdded)
                    await _store.DeleteAsync(id, CancellationToken.None).ConfigureAwait(false);
                if (File.Exists(absolute)) File.Delete(absolute);
                throw;
            }
    }

    public Task<IReadOnlyList<LocalFileSnapshot>> GetSnapshotsAsync(InstallationId installationId, CancellationToken cancellationToken) => _store.GetAsync(installationId, cancellationToken);

    public Task<IReadOnlyList<LocalFileSnapshot>> GetSnapshotsForEntryAsync(Guid environmentEntryId, CancellationToken cancellationToken) => _store.GetForEntryAsync(environmentEntryId, cancellationToken);

    public async Task<string?> GetSnapshotPathAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        var snapshot = await _store.GetAsync(snapshotId, cancellationToken).ConfigureAwait(false);
        if (snapshot is null) return null;
        var path = GetAbsoluteSnapshotPath(snapshot);
        return File.Exists(path) ? path : null;
    }

    public async Task DeleteSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        var snapshot = await _store.GetAsync(snapshotId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Snapshot was not found.");
        var gate = _locks.GetOrAdd(snapshot.OriginalPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var path = GetAbsoluteSnapshotPath(snapshot);
            if (File.Exists(path)) File.Delete(path);
            await _store.DeleteAsync(snapshotId, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async Task<LocalFileSnapshot> RestoreSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken)
    {
        var snapshot = await _store.GetAsync(snapshotId, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Snapshot was not found.");
        var snapshotPath = GetAbsoluteSnapshotPath(snapshot);
        if (!File.Exists(snapshotPath)) throw new FileNotFoundException("Snapshot bytes are missing.", snapshotPath);
        var gate = _locks.GetOrAdd(snapshot.OriginalPath, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(snapshot.OriginalPath))
            {
                var current = await _environment.GetEnvironmentAsync(snapshot.InstallationId, cancellationToken).ConfigureAwait(false);
                var entry = current.Entries.FirstOrDefault(x => x.Id == snapshot.EnvironmentEntryId);
                if (entry is not null && SnapshotEligibilityPolicy.Evaluate(entry).IsEligible)
                {
                    var eligibility = SnapshotEligibilityPolicy.Evaluate(entry);
                    var sourcePath = Path.GetFullPath(entry.Path);
                    await CreateSnapshotCoreAsync(snapshot.InstallationId, snapshot.EnvironmentEntryId, entry, eligibility, LocalFileSnapshotReason.PreRestore, sourcePath, cancellationToken).ConfigureAwait(false);
                }
                else
                    throw new IOException("Current file cannot be safely backed up before restore.");
            }

            var parent = Path.GetDirectoryName(snapshot.OriginalPath);
            if (string.IsNullOrWhiteSpace(parent)) throw new IOException("Snapshot original path has no valid parent directory.");
            Directory.CreateDirectory(parent);
            var temp = snapshot.OriginalPath + ".playstead-restore-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await CopyAsync(snapshotPath, temp, cancellationToken).ConfigureAwait(false);
                File.Move(temp, snapshot.OriginalPath, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return snapshot;
        }
        finally { gate.Release(); }
    }

    private async Task PurgeAsync(InstallationId installationId, Guid entryId, CancellationToken cancellationToken)
    {
        var entries = (await _store.GetForEntryAsync(entryId, cancellationToken).ConfigureAwait(false)).OrderByDescending(x => x.CreatedAtUtc).ToArray();
        foreach (var old in entries.Skip(_retentionPerFile)) await DeleteSnapshotCoreAsync(old, cancellationToken).ConfigureAwait(false);
        var all = (await _store.GetAsync(installationId, cancellationToken).ConfigureAwait(false)).Where(x => File.Exists(GetAbsoluteSnapshotPath(x))).ToArray();
        var total = all.Sum(x => x.SourceSizeBytes);
        if (total <= _quotaBytes) return;
        foreach (var candidate in all.OrderBy(x => x.IsPreRestoreBackup).ThenBy(x => x.CreatedAtUtc))
        {
            if (total <= _quotaBytes) break;
            await DeleteSnapshotCoreAsync(candidate, cancellationToken).ConfigureAwait(false);
            total -= candidate.SourceSizeBytes;
        }
    }

    private async Task DeleteSnapshotCoreAsync(LocalFileSnapshot snapshot, CancellationToken cancellationToken)
    {
        var path = GetAbsoluteSnapshotPath(snapshot);
        if (File.Exists(path)) File.Delete(path);
        await _store.DeleteAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
    }

    private string GetAbsoluteSnapshotPath(LocalFileSnapshot snapshot) => GetAbsoluteSnapshotPath(snapshot.SnapshotRelativePath);
    private string GetAbsoluteSnapshotPath(string relative)
    {
        var candidate = Path.GetFullPath(Path.Combine(_root, relative));
        var relativeToRoot = Path.GetRelativePath(_root, candidate);
        if (relativeToRoot == ".." || relativeToRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relativeToRoot))
            throw new InvalidOperationException("Snapshot path escapes the dedicated storage root.");
        return candidate;
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static async Task CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
