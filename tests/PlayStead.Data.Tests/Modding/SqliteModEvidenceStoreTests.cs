using PlayStead.Core.Library;
using PlayStead.Core.Modding;
using PlayStead.Data.Database;
using PlayStead.Data.Modding;

namespace PlayStead.Data.Tests.Modding;

public sealed class SqliteModEvidenceStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "ModEvidence", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Evidence_round_trips_and_removal_clears_state()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "db.sqlite"), Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var store = new SqliteModEvidenceStore(options);
        var game = GameId.New();
        var evidence = new ModEvidence(game, ProviderKind.Steam, "steam-workshop-content", ModEvidenceKind.WorkshopContentPresent, ModDetectionState.PossiblyModded, DateTimeOffset.UtcNow, "item");
        await store.UpsertAsync(evidence, CancellationToken.None);
        var loaded = Assert.Single(await store.GetByGameAsync(game, CancellationToken.None));
        Assert.Equal(evidence, loaded);
        await store.RemoveAsync(game, evidence.DetectorId, evidence.EvidenceKind, CancellationToken.None);
        Assert.Empty(await store.GetByGameAsync(game, CancellationToken.None));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
