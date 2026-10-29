using PlayStead.Core.Library;
using PlayStead.Core.LocalEnvironment;

namespace PlayStead.Core.Tests.LocalEnvironment;

public sealed class LocalEnvironmentSnapshotServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Snapshots-" + Guid.NewGuid().ToString("N"));
    private readonly InstallationId _installation = InstallationId.New();

    [Fact]
    public async Task Creates_lists_deletes_and_restores_snapshot_with_pre_restore_backup()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "GameUserSettings.ini");
        await File.WriteAllTextAsync(source, "v1");
        var entry = Entry(source);
        var env = new FakeEnvironment([entry]);
        var store = new MemoryStore();
        var service = new LocalEnvironmentSnapshotService(env, store, Path.Combine(_root, "snapshots"), quotaBytes: 10_000, retentionPerFile: 5);

        var first = await service.CreateSnapshotAsync(_installation, entry.Id, LocalFileSnapshotReason.Manual, CancellationToken.None);
        Assert.Equal("v1", await File.ReadAllTextAsync(Path.Combine(_root, "snapshots", first.SnapshotRelativePath)));
        Assert.Equal(64, first.Sha256.Length);
        await File.WriteAllTextAsync(source, "v2");
        var restored = await service.RestoreSnapshotAsync(first.Id, CancellationToken.None);
        Assert.Equal(first.Id, restored.Id);
        Assert.Equal("v1", await File.ReadAllTextAsync(source));
        Assert.Contains(await service.GetSnapshotsForEntryAsync(entry.Id, CancellationToken.None), x => x.IsPreRestoreBackup);
        await service.DeleteSnapshotAsync(first.Id, CancellationToken.None);
        Assert.DoesNotContain(first.Id, (await service.GetSnapshotsAsync(_installation, CancellationToken.None)).Select(x => x.Id));
    }

    [Fact]
    public async Task Rejects_unsupported_oversized_missing_and_non_config_entries()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "file.ini");
        await File.WriteAllTextAsync(source, "ok");
        var env = new FakeEnvironment([Entry(source), Entry(Path.Combine(_root, "file.dll"), LocalEnvironmentEntryKind.Config), Entry(Path.Combine(_root, "folder.ini"), LocalEnvironmentEntryKind.Config, LocalEnvironmentPathKind.Directory), Entry(Path.Combine(_root, "missing.ini"), LocalEnvironmentEntryKind.Config, LocalEnvironmentPathKind.File, false)]);
        var service = new LocalEnvironmentSnapshotService(env, new MemoryStore(), Path.Combine(_root, "snapshots"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateSnapshotAsync(_installation, env.Entries[1].Id, LocalFileSnapshotReason.Manual, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateSnapshotAsync(_installation, env.Entries[2].Id, LocalFileSnapshotReason.Manual, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateSnapshotAsync(_installation, env.Entries[3].Id, LocalFileSnapshotReason.Manual, CancellationToken.None));
    }

    [Fact]
    public async Task Enforces_quota_and_retention_per_file()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "settings.ini");
        var entry = Entry(source);
        var env = new FakeEnvironment([entry]);
        var store = new MemoryStore();
        var service = new LocalEnvironmentSnapshotService(env, store, Path.Combine(_root, "snapshots"), quotaBytes: 10, retentionPerFile: 2);
        await File.WriteAllTextAsync(source, "12345"); await service.CreateSnapshotAsync(_installation, entry.Id, LocalFileSnapshotReason.Manual, CancellationToken.None);
        await File.WriteAllTextAsync(source, "67890"); await service.CreateSnapshotAsync(_installation, entry.Id, LocalFileSnapshotReason.Manual, CancellationToken.None);
        await File.WriteAllTextAsync(source, "abcde"); await service.CreateSnapshotAsync(_installation, entry.Id, LocalFileSnapshotReason.Manual, CancellationToken.None);
        Assert.True((await service.GetSnapshotsForEntryAsync(entry.Id, CancellationToken.None)).Count <= 2);
    }

    [Fact]
    public async Task Supports_missing_original_parent_and_cancellation()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "nested", "settings.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!); await File.WriteAllTextAsync(source, "v1");
        var entry = Entry(source); var store = new MemoryStore(); var env = new FakeEnvironment([entry]);
        var service = new LocalEnvironmentSnapshotService(env, store, Path.Combine(_root, "snapshots"));
        var snapshot = await service.CreateSnapshotAsync(_installation, entry.Id, LocalFileSnapshotReason.Manual, CancellationToken.None);
        File.Delete(source); Directory.Delete(Path.GetDirectoryName(source)!);
        await service.RestoreSnapshotAsync(snapshot.Id, CancellationToken.None);
        Assert.Equal("v1", await File.ReadAllTextAsync(source));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetSnapshotsAsync(_installation, cts.Token));
    }

    private LocalEnvironmentEntry Entry(string path, LocalEnvironmentEntryKind kind = LocalEnvironmentEntryKind.Config, LocalEnvironmentPathKind pathKind = LocalEnvironmentPathKind.File, bool exists = true) => new(Guid.NewGuid(), _installation, kind, path, pathKind, LocalEnvironmentConfidence.Detected, LocalEnvironmentSource.Catalog, exists, DateTimeOffset.UtcNow, exists ? DateTimeOffset.UtcNow : null);

    private sealed class FakeEnvironment(IReadOnlyList<LocalEnvironmentEntry> entries) : ILocalEnvironmentDiscoveryService
    {
        public IReadOnlyList<LocalEnvironmentEntry> Entries { get; } = entries;
        public Task<GameLocalEnvironment> GetEnvironmentAsync(InstallationId installationId, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(new GameLocalEnvironment(installationId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Entries.Select(x => x with { InstallationId = installationId }).ToArray())); }
        public Task<GameLocalEnvironment> RefreshEnvironmentAsync(InstallationId installationId, CancellationToken token) => GetEnvironmentAsync(installationId, token);
        public Task RemoveUserOverrideAsync(InstallationId installationId, LocalEnvironmentEntryKind kind, string path, CancellationToken token) => Task.CompletedTask;
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetConfigsAsync(InstallationId installationId, CancellationToken token) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>(Entries.Where(x => x.Kind == LocalEnvironmentEntryKind.Config).ToArray());
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetLogsAsync(InstallationId installationId, CancellationToken token) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);
        public Task<IReadOnlyList<LocalEnvironmentEntry>> GetSavesAsync(InstallationId installationId, CancellationToken token) => Task.FromResult<IReadOnlyList<LocalEnvironmentEntry>>([]);
        public Task<LocalEnvironmentCustomizationState> GetCustomizationStateAsync(InstallationId installationId, CancellationToken token) => Task.FromResult(LocalEnvironmentCustomizationState.Indeterminate);
    }

    private sealed class MemoryStore : ILocalFileSnapshotStore
    {
        private readonly Dictionary<Guid, LocalFileSnapshot> _items = [];
        public Task<LocalFileSnapshot?> GetAsync(Guid id, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(_items.GetValueOrDefault(id)); }
        public Task<IReadOnlyList<LocalFileSnapshot>> GetAsync(InstallationId id, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<LocalFileSnapshot>>(_items.Values.Where(x => x.InstallationId == id).OrderByDescending(x => x.CreatedAtUtc).ToArray()); }
        public Task<IReadOnlyList<LocalFileSnapshot>> GetForEntryAsync(Guid id, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<LocalFileSnapshot>>(_items.Values.Where(x => x.EnvironmentEntryId == id).OrderByDescending(x => x.CreatedAtUtc).ToArray()); }
        public Task AddAsync(LocalFileSnapshot snapshot, CancellationToken token) { token.ThrowIfCancellationRequested(); _items[snapshot.Id] = snapshot; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken token) { token.ThrowIfCancellationRequested(); _items.Remove(id); return Task.CompletedTask; }
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
