using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class DatabaseHealthCheckerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task QuickCheck_returns_healthy_for_initialized_database()
    {
        Directory.CreateDirectory(_root);

        var databasePath = Path.Combine(_root, "playstead.db");
        var backupsDirectory = Path.Combine(_root, "Backups");
        var options = new DatabaseOptions(databasePath, backupsDirectory);

        var initializer = new DatabaseInitializer(options);
        await initializer.InitializeAsync(CancellationToken.None);

        var sut = new DatabaseHealthChecker(options);

        var result = await sut.QuickCheckAsync(CancellationToken.None);

        Assert.True(result.IsHealthy);
        Assert.Equal("ok", result.Detail);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
