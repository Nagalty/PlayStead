using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Data.Database;
using PlayStead.Data.LocalArtifacts;

namespace PlayStead.Data.Tests.LocalArtifacts;

public sealed class SqliteLocalArtifactSnapshotStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "SnapshotsData", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Snapshots_round_trip_order_and_delete()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "db.sqlite"), Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var game = GameId.New();
        var store = new SqliteLocalArtifactSnapshotStore(options);
        var old = new LocalArtifactSnapshot(Guid.NewGuid(), game, GameLocalArtifactKind.Configuration, "rule", Path.Combine(_root, "old.zip"), "SHA256", "A", 1, 1, DateTimeOffset.UtcNow.AddDays(-1));
        var current = old with { SnapshotId = Guid.NewGuid(), ArchivePath = Path.Combine(_root, "current.zip"), FingerprintHash = "B", CreatedAtUtc = DateTimeOffset.UtcNow };
        await store.UpsertAsync(old, CancellationToken.None);
        await store.UpsertAsync(current, CancellationToken.None);
        var values = await store.GetAsync(game, old.ArtifactKind, old.RuleIdentity, CancellationToken.None);
        Assert.Equal([current.SnapshotId, old.SnapshotId], values.Select(x => x.SnapshotId));
        await store.DeleteAsync(old.SnapshotId, CancellationToken.None);
        Assert.Single(await store.GetAsync(game, old.ArtifactKind, old.RuleIdentity, CancellationToken.None));
        await store.DeleteAsync(old.SnapshotId, CancellationToken.None);
    }

    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
