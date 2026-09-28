using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Database;

public sealed class NotificationMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        "NotificationMigration",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Fresh_database_finishes_at_schema_version_9_with_notifications_table()
    {
        var options = await CreateDatabaseAsync("fresh.db");

        await using var connection = await OpenAsync(options.DatabasePath);
        Assert.Equal(19, await ScalarIntAsync(connection,
            "SELECT MAX(version) FROM schema_migrations;"));
        Assert.Equal(1, await ScalarIntAsync(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='notifications';"));
    }

    [Fact]
    public async Task V8_upgrade_preserves_existing_game_and_identity_rows()
    {
        var options = await CreateDatabaseAtVersion8Async("upgrade.db");
        var gameId = "11111111-1111-4111-8111-111111111111";

        await using (var connection = await OpenAsync(options.DatabasePath))
        {
            await ExecuteAsync(connection,
                $"INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES ('{gameId}','Existing',0,'2026-09-16T18:00:00Z','2026-09-16T18:00:00Z');");
            await ExecuteAsync(connection,
                $"INSERT INTO game_identity_resolutions(game_id,provisional_id,state,candidate_content_id,evidence_json,created_utc,updated_utc) VALUES ('{gameId}','PS-TEMP-01K5C0YQ8S0000000000000000',4,NULL,'{{}}','2026-09-16T18:00:00Z','2026-09-16T18:00:00Z');");
            Assert.Equal(
19,await ScalarIntAsync(connection,
                "SELECT MAX(version) FROM schema_migrations;"));
        }

        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);

        await using var upgraded = await OpenAsync(options.DatabasePath);
        Assert.Equal(19, await ScalarIntAsync(upgraded,
            "SELECT MAX(version) FROM schema_migrations;"));
        Assert.Equal(1, await ScalarIntAsync(upgraded,
            $"SELECT COUNT(*) FROM games WHERE game_id='{gameId}';"));
        Assert.Equal(1, await ScalarIntAsync(upgraded,
            $"SELECT COUNT(*) FROM game_identity_resolutions WHERE game_id='{gameId}';"));
    }

    [Fact]
    public async Task Deduplication_key_is_unique_but_subject_and_reason_can_repeat()
    {
        await using var connection = await OpenNotificationsAsync("unique.db");
        await InsertAsync(connection, "n1", "subject", "reason", "key-1", 1, 1, null, null);
        await InsertAsync(connection, "n2", "subject", "reason", "key-2", 1, 1, null, null);
        await Assert.ThrowsAnyAsync<SqliteException>(() =>
            InsertAsync(connection, "n3", "other", "other", "key-1", 1, 1, null, null));
    }

    [Theory]
    [InlineData("producer", 2)]
    [InlineData("priority", 4)]
    [InlineData("state", 4)]
    public async Task Unknown_enum_values_are_rejected(string column, int value)
    {
        await using var connection = await OpenNotificationsAsync(column + ".db");
        await Assert.ThrowsAnyAsync<SqliteException>(() =>
            InsertAsync(connection, "n1", "subject", "reason", "key", 1, 1, null, null, column, value));
    }

    [Theory]
    [InlineData(1, "2026-09-16T18:00:00Z", null)]
    [InlineData(1, null, "2026-09-16T18:00:00Z")]
    [InlineData(2, null, null)]
    [InlineData(2, "2026-09-16T18:00:00Z", "2026-09-16T18:00:00Z")]
    [InlineData(3, "2026-09-16T18:00:00Z", null)]
    public async Task Invalid_lifecycle_timestamp_combinations_are_rejected(
        int state,
        string? readUtc,
        string? resolvedUtc)
    {
        await using var connection = await OpenNotificationsAsync($"timestamps-{state}-{Guid.NewGuid():N}.db");
        await Assert.ThrowsAnyAsync<SqliteException>(() =>
            InsertAsync(connection, "n1", "subject", "reason", "key", 1, state, readUtc, resolvedUtc));
    }

    [Fact]
    public async Task Resolved_notification_may_have_null_read_timestamp_and_payload_is_nullable()
    {
        await using var connection = await OpenNotificationsAsync("resolved.db");
        await InsertAsync(connection, "n1", "subject", "reason", "key", 1, 3, null, "2026-09-16T18:00:00Z");
        Assert.Equal(1, await ScalarIntAsync(connection,
            "SELECT COUNT(*) FROM notifications WHERE payload_json IS NULL;"));
    }

    [Fact]
    public async Task Schema_has_no_history_table_or_cross_database_foreign_key()
    {
        await using var connection = await OpenNotificationsAsync("shape.db");
        Assert.Equal(0, await ScalarIntAsync(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('notification_history','notification_occurrences','notification_events');"));
        var foreignKeys = await QueryTextAsync(connection,
            "SELECT sql FROM sqlite_master WHERE type='table' AND name='notifications';");
        Assert.DoesNotContain("catalog.db", foreignKeys, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<DatabaseOptions> CreateDatabaseAsync(string fileName)
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(
            Path.Combine(_root, fileName),
            Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        return options;
    }

    private async Task<DatabaseOptions> CreateDatabaseAtVersion8Async(string fileName)
    {
        var options = await CreateDatabaseAsync(fileName);
        await using var connection = await OpenAsync(options.DatabasePath);
        await ExecuteAsync(connection, "DROP TABLE notifications;");
        await ExecuteAsync(connection, "DELETE FROM schema_migrations WHERE version IN (9, 10);");
        await ExecuteAsync(connection, "DROP INDEX IF EXISTS ux_game_identity_decisions_active_confirm; DROP INDEX IF EXISTS ux_game_identity_decisions_active_candidate; DROP TABLE IF EXISTS game_identity_decisions;");
        return options;
    }

    private async Task<SqliteConnection> OpenNotificationsAsync(string fileName)
    {
        var options = await CreateDatabaseAsync(fileName);
        var connection = await OpenAsync(options.DatabasePath);
        return connection;
    }

    private static async Task<SqliteConnection> OpenAsync(string path)
    {
        var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task InsertAsync(
        SqliteConnection connection,
        string id,
        string subject,
        string reason,
        string key,
        int producer,
        int state,
        string? readUtc,
        string? resolvedUtc,
        string? overrideColumn = null,
        int? overrideValue = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO notifications(
                notification_id, producer, subject_id, reason, deduplication_key,
                priority, state, title, message, payload_json,
                created_utc, updated_utc, read_utc, resolved_utc)
            VALUES($id, $producer, $subject, $reason, $key,
                $priority, $state, 'Title', 'Message', NULL,
                '2026-09-16T18:00:00Z', '2026-09-16T18:00:00Z', $read, $resolved);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$producer", overrideColumn == "producer" ? overrideValue!.Value : producer);
        command.Parameters.AddWithValue("$subject", subject);
        command.Parameters.AddWithValue("$reason", reason);
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$priority", overrideColumn == "priority" ? overrideValue!.Value : 1);
        command.Parameters.AddWithValue("$state", overrideColumn == "state" ? overrideValue!.Value : state);
        command.Parameters.AddWithValue("$read", (object?)readUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("$resolved", (object?)resolvedUtc ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarIntAsync(SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<string> QueryTextAsync(SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
