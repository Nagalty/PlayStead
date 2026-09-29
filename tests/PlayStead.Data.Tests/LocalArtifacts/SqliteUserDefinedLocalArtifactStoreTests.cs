using PlayStead.Core.Library;
using PlayStead.Core.LocalArtifacts;
using PlayStead.Data.Database;
using PlayStead.Data.LocalArtifacts;

namespace PlayStead.Data.Tests.LocalArtifacts;

public sealed class SqliteUserDefinedLocalArtifactStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "UserArtifacts", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task User_defined_artifacts_persist_and_distinct_same_kind_paths_coexist()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "db.sqlite"), Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var game = GameId.New();
        var first = Path.Combine(_root, "saves-a");
        var second = Path.Combine(_root, "saves-b");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        var store = new SqliteUserDefinedLocalArtifactStore(options);
        await store.AddAsync(new(Guid.NewGuid(), game, GameLocalArtifactKind.SaveData, first, null, DateTimeOffset.UtcNow.AddMinutes(-1)), CancellationToken.None);
        await store.AddAsync(new(Guid.NewGuid(), game, GameLocalArtifactKind.SaveData, second, "Profil", DateTimeOffset.UtcNow), CancellationToken.None);

        var reloaded = new SqliteUserDefinedLocalArtifactStore(options);
        var values = await reloaded.GetByGameAsync(game, CancellationToken.None);
        Assert.Equal(2, values.Count);
        Assert.Contains(values, item => item.Path == first);
        Assert.Contains(values, item => item.Path == second && item.DisplayName == "Profil");
        await reloaded.RemoveAsync(values[0].Id, CancellationToken.None);
        Assert.Single(await reloaded.GetByGameAsync(game, CancellationToken.None));
        Assert.True(Directory.Exists(first));
        Assert.True(Directory.Exists(second));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
