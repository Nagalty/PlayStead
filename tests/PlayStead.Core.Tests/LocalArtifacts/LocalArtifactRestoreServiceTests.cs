using System.IO.Compression;
using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;

namespace PlayStead.Core.Tests.LocalArtifacts;

public sealed class LocalArtifactRestoreServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "Restore", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task File_restore_creates_safety_snapshot_and_restores_content()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(path, "old");
        var artifact = Artifact(path, GameLocalArtifactKind.Configuration);
        var store = new MemoryStore();
        var snapshots = new LocalArtifactSnapshotService(new Sha256ArtifactFingerprintService(), store, Path.Combine(_root, "snapshots"));
        var snapshot = await snapshots.CreateAsync(artifact, CancellationToken.None);
        await File.WriteAllTextAsync(path, "current");

        var result = await new LocalArtifactRestoreService(new Sha256ArtifactFingerprintService(), snapshots)
            .RestoreAsync(artifact, snapshot, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("old", await File.ReadAllTextAsync(path));
        Assert.NotNull(result.SafetySnapshot);
        Assert.Equal(2, store.Items.Count);
    }

    [Fact]
    public async Task Missing_archive_is_rejected_without_changing_source()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(path, "current");
        var snapshot = new LocalArtifactSnapshot(Guid.NewGuid(), GameId.New(), GameLocalArtifactKind.Configuration, "rule", Path.Combine(_root, "missing.zip"), "SHA256", "hash", 1, 7, DateTimeOffset.UtcNow);
        var result = await new LocalArtifactRestoreService(new Sha256ArtifactFingerprintService(), new NoopSnapshotService())
            .RestoreAsync(Artifact(path, GameLocalArtifactKind.Configuration, snapshot.GameId), snapshot, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal("current", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Running_game_is_refused_before_snapshot_creation()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(path, "current");
        var snapshot = new LocalArtifactSnapshot(Guid.NewGuid(), GameId.New(), GameLocalArtifactKind.Configuration, "rule", Path.Combine(_root, "snapshot.zip"), "SHA256", "hash", 1, 7, DateTimeOffset.UtcNow);
        var result = await new LocalArtifactRestoreService(new Sha256ArtifactFingerprintService(), new ThrowingSnapshotService(), _ => true)
            .RestoreAsync(Artifact(path, GameLocalArtifactKind.Configuration, snapshot.GameId), snapshot, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Contains("Fermez", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Zip_slip_is_rejected()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(path, "current");
        var zip = Path.Combine(_root, "bad.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open())) await writer.WriteAsync("bad");
        var gameId = GameId.New();
        var snapshot = new LocalArtifactSnapshot(Guid.NewGuid(), gameId, GameLocalArtifactKind.Configuration, "rule", zip, "SHA256", "bad", 1, 3, DateTimeOffset.UtcNow);
        var result = await new LocalArtifactRestoreService(new Sha256ArtifactFingerprintService(), new NoopSnapshotService())
            .RestoreAsync(Artifact(path, GameLocalArtifactKind.Configuration, gameId), snapshot, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal("current", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Post_restore_fingerprint_mismatch_rolls_back_original_file_and_cleans_temporaries()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(path, "original");
        var zip = Path.Combine(_root, "target.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("settings.json").Open())) await writer.WriteAsync("restored");
        var gameId = GameId.New();
        var snapshot = new LocalArtifactSnapshot(Guid.NewGuid(), gameId, GameLocalArtifactKind.Configuration, "rule", zip, "SHA256", "target", 1, 8, DateTimeOffset.UtcNow);
        var fingerprint = new SequencedFingerprint("target", "different");
        var result = await new LocalArtifactRestoreService(fingerprint, new NoopSnapshotService())
            .RestoreAsync(Artifact(path, GameLocalArtifactKind.Configuration, gameId), snapshot, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("original", await File.ReadAllTextAsync(path));
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(_root), x => Path.GetFileName(x).StartsWith(".playstead-restore-", StringComparison.Ordinal));
    }

    private GameLocalArtifact Artifact(string path, GameLocalArtifactKind kind, GameId? gameId = null) =>
        new(gameId ?? GameId.New(), kind, path, GameLocalArtifactSource.ExplicitRule, GameLocalArtifactStatus.KnownAndExists, "rule", LocalArtifactBaselineStatus.Unchanged);

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class MemoryStore : ILocalArtifactSnapshotStore
    {
        public List<LocalArtifactSnapshot> Items { get; } = [];
        public Task<IReadOnlyList<LocalArtifactSnapshot>> GetAsync(GameId gameId, GameLocalArtifactKind kind, string ruleIdentity, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalArtifactSnapshot>>(Items);
        public Task UpsertAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken) { Items.Add(snapshot); return Task.CompletedTask; }
        public Task DeleteAsync(Guid snapshotId, CancellationToken cancellationToken) { Items.RemoveAll(x => x.SnapshotId == snapshotId); return Task.CompletedTask; }
    }
    private sealed class NoopSnapshotService : ILocalArtifactSnapshotService
    {
        public Task<LocalArtifactSnapshot> CreateAsync(GameLocalArtifact artifact, CancellationToken cancellationToken, SnapshotReason reason = SnapshotReason.Manual) => Task.FromResult(new LocalArtifactSnapshot(Guid.NewGuid(), artifact.GameId, artifact.Kind, artifact.RuleIdentity!, "", "", "", 0, 0, DateTimeOffset.UtcNow, true, reason));
        public Task<IReadOnlyList<LocalArtifactSnapshot>> ListAsync(GameLocalArtifact artifact, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalArtifactSnapshot>>([]);
        public Task DeleteAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class ThrowingSnapshotService : ILocalArtifactSnapshotService
    {
        public Task<LocalArtifactSnapshot> CreateAsync(GameLocalArtifact artifact, CancellationToken cancellationToken, SnapshotReason reason = SnapshotReason.Manual) => throw new IOException();
        public Task<IReadOnlyList<LocalArtifactSnapshot>> ListAsync(GameLocalArtifact artifact, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task DeleteAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class SequencedFingerprint(string extractedHash, string finalHash) : IArtifactFingerprintService
    {
        private int _calls;
        public Task<ArtifactFingerprintResult> ComputeAsync(GameLocalArtifact artifact, CancellationToken cancellationToken)
        {
            var hash = Interlocked.Increment(ref _calls) == 1 ? extractedHash : finalHash;
            return Task.FromResult(new ArtifactFingerprintResult(new ArtifactFingerprint("SHA256", hash, 1, 1, DateTimeOffset.UtcNow), null));
        }
    }
}
