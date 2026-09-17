using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlaySteadTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Initialize_creates_current_schema_and_is_idempotent()
    {
        Directory.CreateDirectory(_root);

        var db = Path.Combine(
            _root,
            "playstead.db");

        var backups = Path.Combine(
            _root,
            "Backups");

        var sut = new DatabaseInitializer(
            new DatabaseOptions(
                db,
                backups));

        await sut.InitializeAsync(CancellationToken.None);
        await sut.InitializeAsync(CancellationToken.None);

        await using var connection = new SqliteConnection(
            $"Data Source={db};Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT MAX(version) FROM schema_migrations;";

        var version = Convert.ToInt32(
            await command.ExecuteScalarAsync());

        Assert.Equal(10, version);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(
                _root,
                recursive: true);
        }
    }
}
