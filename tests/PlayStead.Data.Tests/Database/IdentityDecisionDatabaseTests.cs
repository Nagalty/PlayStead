using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class IdentityDecisionDatabaseTests
{
    [Fact]
    public async Task Fresh_database_reaches_v10_and_has_decision_schema()
    {
        using var fixture = new TestDatabase();
        await new DatabaseInitializer(fixture.Options).InitializeAsync(CancellationToken.None);

        await using var connection = await fixture.OpenAsync();
        Assert.Equal(17, await ScalarAsync(connection, "SELECT MAX(version) FROM schema_migrations;"));
        Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='game_identity_decisions';"));
        Assert.Equal(2L, await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name LIKE 'ux_game_identity_decisions_active_%';"));
    }

    [Fact]
    public async Task Active_indexes_enforce_confirm_and_candidate_invariants()
    {
        using var fixture = new TestDatabase();
        await new DatabaseInitializer(fixture.Options).InitializeAsync(CancellationToken.None);
        await using var connection = await fixture.OpenAsync();
        await ExecuteAsync(connection, "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES ('g','G',0,'u','u');");
        await ExecuteAsync(connection, "INSERT INTO game_identity_decisions VALUES ('1','g','a',1,'u','u',NULL);");
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(connection, "INSERT INTO game_identity_decisions VALUES ('2','g','b',1,'u','u',NULL);"));
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(connection, "INSERT INTO game_identity_decisions VALUES ('3','g','a',2,'u','u',NULL);"));
        await ExecuteAsync(connection, "INSERT INTO game_identity_decisions VALUES ('4','g','b',2,'u','u',NULL);");
    }

    [Fact]
    public async Task Revoked_history_allows_reuse_and_is_preserved()
    {
        using var fixture = new TestDatabase();
        await new DatabaseInitializer(fixture.Options).InitializeAsync(CancellationToken.None);
        await using var connection = await fixture.OpenAsync();
        await ExecuteAsync(connection, "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES ('g','G',0,'u','u');");
        await ExecuteAsync(connection, "INSERT INTO game_identity_decisions VALUES ('1','g','a',1,'u','u','r'); INSERT INTO game_identity_decisions VALUES ('2','g','a',2,'u','u',NULL);");
        Assert.Equal(2L, await ScalarAsync(connection, "SELECT COUNT(*) FROM game_identity_decisions WHERE game_id='g';"));
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestDatabase : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", "IdentityDecision", Guid.NewGuid().ToString("N"));
        public TestDatabase() { Directory.CreateDirectory(_root); Options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "Backups")); }
        public DatabaseOptions Options { get; }
        public async Task<SqliteConnection> OpenAsync() { var c = new SqliteConnection($"Data Source={Options.DatabasePath};Pooling=False;Foreign Keys=True"); await c.OpenAsync(); return c; }
        public void Dispose() { SqliteConnection.ClearAllPools(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}
