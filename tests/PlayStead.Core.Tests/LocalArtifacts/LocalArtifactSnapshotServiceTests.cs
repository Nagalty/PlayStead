using System.IO.Compression;
using System.Text.Json;
using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;

namespace PlayStead.Core.Tests.LocalArtifacts;

public sealed class LocalArtifactSnapshotServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "Snapshots", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task File_snapshot_contains_metadata_and_source_content()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(source, "{\"enabled\":true}");
        var artifact = new GameLocalArtifact(GameId.New(), GameLocalArtifactKind.Configuration, source, GameLocalArtifactSource.ExplicitRule, GameLocalArtifactStatus.KnownAndExists, "rule", LocalArtifactBaselineStatus.Unchanged);
        var store = new MemoryStore();
        var snapshot = await new LocalArtifactSnapshotService(new Sha256ArtifactFingerprintService(), store, Path.Combine(_root, "Snapshots")).CreateAsync(artifact, CancellationToken.None);

        Assert.True(File.Exists(snapshot.ArchivePath));
        using var archive = ZipFile.OpenRead(snapshot.ArchivePath);
        var metadata = archive.GetEntry("metadata.json");
        Assert.NotNull(metadata);
        using var reader = new StreamReader(metadata!.Open());
        var json = await reader.ReadToEndAsync();
        Assert.Contains(snapshot.FingerprintHash, json, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(archive.GetEntry("settings.json"));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(snapshot.ArchivePath)!, "*.tmp"));
    }

    [Fact]
    public async Task Missing_source_fails_without_final_archive()
    {
        Directory.CreateDirectory(_root);
        var missing = Path.Combine(_root, "missing");
        var artifact = new GameLocalArtifact(GameId.New(), GameLocalArtifactKind.SaveData, missing, GameLocalArtifactSource.ExplicitRule, GameLocalArtifactStatus.KnownButMissing, "rule");
        var snapshots = Path.Combine(_root, "Snapshots");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LocalArtifactSnapshotService(new Sha256ArtifactFingerprintService(), new MemoryStore(), snapshots).CreateAsync(artifact, CancellationToken.None));
        Assert.False(Directory.Exists(snapshots));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class MemoryStore : ILocalArtifactSnapshotStore
    {
        public Task<IReadOnlyList<LocalArtifactSnapshot>> GetAsync(GameId gameId, GameLocalArtifactKind kind, string ruleIdentity, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalArtifactSnapshot>>([]);
        public Task<IReadOnlyList<LocalArtifactSnapshot>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<LocalArtifactSnapshot>>([]);
        public Task UpsertAsync(LocalArtifactSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid snapshotId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
