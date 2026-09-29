using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;

namespace PlayStead.Core.Tests.LocalArtifacts;

public sealed class LocalSnapshotStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "Quota", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Usage_counts_actual_archive_bytes_and_oldest_snapshot()
    {
        Directory.CreateDirectory(_root);
        var store = new MemoryStore();
        var first = await AddSnapshotAsync(store, 7, DateTimeOffset.UtcNow.AddMinutes(-2));
        var second = await AddSnapshotAsync(store, 3, DateTimeOffset.UtcNow.AddMinutes(-1));
        var storage = new LocalSnapshotStorageService(store, 100);

        var usage = await storage.GetUsageAsync(CancellationToken.None);

        Assert.Equal(10, usage.UsedBytes);
        Assert.Equal(2, usage.SnapshotCount);
        Assert.Equal(first.CreatedAtUtc, usage.OldestSnapshotUtc);
    }

    [Fact]
    public async Task Retention_removes_oldest_but_keeps_latest_snapshot_per_artifact()
    {
        Directory.CreateDirectory(_root);
        var store = new MemoryStore();
        var gameId = GameId.New();
        var old = await AddSnapshotAsync(store, 7, DateTimeOffset.UtcNow.AddMinutes(-2), gameId: gameId);
        var latest = await AddSnapshotAsync(store, 7, DateTimeOffset.UtcNow.AddMinutes(-1), gameId: gameId);
        var storage = new LocalSnapshotStorageService(store, 10);

        await storage.ApplyRetentionAsync(CancellationToken.None);

        Assert.False(File.Exists(old.ArchivePath));
        Assert.True(File.Exists(latest.ArchivePath));
        Assert.Single(store.Items);
    }

    [Fact]
    public async Task Oversized_candidate_is_rejected_and_archive_removed()
    {
        Directory.CreateDirectory(_root);
        var store = new MemoryStore();
        var candidate = await AddSnapshotAsync(store, 20, DateTimeOffset.UtcNow, addToStore: false);
        var storage = new LocalSnapshotStorageService(store, 10);

        await Assert.ThrowsAsync<SnapshotStorageQuotaExceededException>(() => storage.EnsureCapacityForAsync(candidate, CancellationToken.None));

        Assert.False(File.Exists(candidate.ArchivePath));
        Assert.Empty(store.Items);
    }

    private async Task<LocalArtifactSnapshot> AddSnapshotAsync(MemoryStore store, int bytes, DateTimeOffset created, bool addToStore = true, GameId? gameId = null)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".zip");
        await File.WriteAllBytesAsync(path, new byte[bytes]);
        var snapshot = new LocalArtifactSnapshot(Guid.NewGuid(), gameId ?? GameId.New(), GameLocalArtifactKind.SaveData, "rule", path, "SHA256", "hash", 1, bytes, created);
        if (addToStore) store.Items.Add(snapshot);
        return snapshot;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class MemoryStore : ILocalArtifactSnapshotStore
    {
        public List<LocalArtifactSnapshot> Items { get; } = [];
        public Task<IReadOnlyList<LocalArtifactSnapshot>> GetAsync(GameId gameId, GameLocalArtifactKind kind, string ruleIdentity, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalArtifactSnapshot>>(Items.Where(x => x.GameId == gameId && x.ArtifactKind == kind && x.RuleIdentity == ruleIdentity).ToArray());
        public Task<IReadOnlyList<LocalArtifactSnapshot>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalArtifactSnapshot>>(Items.ToArray());
        public Task UpsertAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken) { Items.Add(snapshot); return Task.CompletedTask; }
        public Task DeleteAsync(Guid snapshotId, CancellationToken cancellationToken) { Items.RemoveAll(x => x.SnapshotId == snapshotId); return Task.CompletedTask; }
    }
}
