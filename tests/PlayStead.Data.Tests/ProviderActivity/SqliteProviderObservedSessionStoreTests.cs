using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;
using PlayStead.Data.Database;
using PlayStead.Data.ProviderActivity;

namespace PlayStead.Data.Tests.ProviderActivity;

public sealed class SqliteProviderObservedSessionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "ProviderObservedSessions", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Provider_session_round_trips_and_period_query_is_idempotent()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var game = GameId.New();
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={options.DatabasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES($id,'Fixture',0,$utc,$utc);";
            command.Parameters.AddWithValue("$id", game.Value.ToString("D"));
            command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        var started = new DateTimeOffset(2026, 9, 28, 9, 52, 13, TimeSpan.Zero);
        var session = new ProviderObservedSession(Guid.NewGuid(), game, ProviderKind.Steam, "1172710", started, started.AddHours(1), "SteamProcessLog", ProviderObservedSessionCompleteness.Complete);
        var store = new SqliteProviderObservedSessionStore(options);
        await store.UpsertAsync(session, CancellationToken.None);
        await store.UpsertAsync(session, CancellationToken.None);

        var values = await store.GetByPeriodAsync(started.AddMinutes(-1), started.AddHours(2), CancellationToken.None);
        var actual = Assert.Single(values);
        Assert.Equal(session, actual);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
