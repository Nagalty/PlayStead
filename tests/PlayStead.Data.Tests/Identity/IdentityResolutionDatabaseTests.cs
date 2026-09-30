using Microsoft.Data.Sqlite;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Identity;

public sealed class IdentityResolutionDatabaseTests : IDisposable
{
    private const string Utc = "2026-09-16T18:00:00.0000000+00:00";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        "Identity",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Fresh_database_has_schema_8_and_identity_resolution_table()
    {
        var databasePath = await CreateCurrentDatabaseAsync("fresh.db");

        await using var connection = await OpenAsync(databasePath);

        Assert.Equal(25,await ScalarAsync(
                connection,
                "SELECT MAX(version) FROM schema_migrations;"));
        Assert.Equal(
            1,
            await ScalarAsync(
                connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='game_identity_resolutions';"));
        Assert.Equal(
            1,
            await ScalarAsync(
                connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='ux_game_identity_resolutions_provisional';"));
        Assert.Equal(
            1,
            await ScalarAsync(
                connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='ix_game_identity_resolutions_candidate';"));
    }

    [Fact]
    public async Task Version7_upgrade_preserves_existing_games()
    {
        var databasePath = Path.Combine(_root, "upgrade.db");
        await CreateVersion7DatabaseAsync(databasePath);
        await InsertGameAsync(
            databasePath,
            "11111111-1111-1111-1111-111111111111",
            "Existing Game");

        await new DatabaseInitializer(CreateOptions(databasePath))
            .InitializeAsync(CancellationToken.None);

        await using var connection = await OpenAsync(databasePath);
        Assert.Equal(25,await ScalarAsync(
                connection,
                "SELECT MAX(version) FROM schema_migrations;"));
        Assert.Equal(
            "Existing Game",
            await TextAsync(
                connection,
                "SELECT title FROM games WHERE game_id='11111111-1111-1111-1111-111111111111';"));
    }

    [Fact]
    public async Task Provisional_id_accepts_null()
    {
        var databasePath = await CreateCurrentDatabaseAsync("nullable.db");
        await InsertGameAsync(databasePath, Game(1), "Game 1");

        await using var connection = await OpenAsync(databasePath);
        await InsertResolutionAsync(
            connection,
            Game(1),
            provisionalId: null,
            state: 4,
            candidateContentId: null);

        Assert.Equal(
            1,
            await ScalarAsync(
                connection,
                "SELECT COUNT(*) FROM game_identity_resolutions WHERE provisional_id IS NULL;"));
    }

    [Fact]
    public async Task Multiple_null_provisional_ids_are_allowed()
    {
        var databasePath = await CreateCurrentDatabaseAsync("multiple-null.db");
        await InsertGameAsync(databasePath, Game(1), "Game 1");
        await InsertGameAsync(databasePath, Game(2), "Game 2");

        await using var connection = await OpenAsync(databasePath);
        await InsertResolutionAsync(connection, Game(1), null, 4, null);
        await InsertResolutionAsync(connection, Game(2), null, 4, null);

        Assert.Equal(
            2,
            await ScalarAsync(
                connection,
                "SELECT COUNT(*) FROM game_identity_resolutions WHERE provisional_id IS NULL;"));
    }

    [Fact]
    public async Task Duplicate_non_null_provisional_id_is_rejected()
    {
        var databasePath = await CreateCurrentDatabaseAsync("duplicate-temp.db");
        await InsertGameAsync(databasePath, Game(1), "Game 1");
        await InsertGameAsync(databasePath, Game(2), "Game 2");

        await using var connection = await OpenAsync(databasePath);
        await AssertIdentityTableExistsAsync(connection);
        const string provisional =
            "PS-TEMP-01M2NP09800000000000000000";
        await InsertResolutionAsync(connection, Game(1), provisional, 4, null);

        await Assert.ThrowsAsync<SqliteException>(
            () => InsertResolutionAsync(
                connection,
                Game(2),
                provisional,
                4,
                null));
    }

    [Fact]
    public async Task Different_games_can_share_candidate_content_id()
    {
        var databasePath = await CreateCurrentDatabaseAsync("shared-candidate.db");
        await InsertGameAsync(databasePath, Game(1), "Game 1");
        await InsertGameAsync(databasePath, Game(2), "Game 2");

        await using var connection = await OpenAsync(databasePath);
        const string candidate =
            "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
        await InsertResolutionAsync(connection, Game(1), null, 2, candidate);
        await InsertResolutionAsync(connection, Game(2), null, 2, candidate);

        Assert.Equal(
            2,
            await ScalarAsync(
                connection,
                $"SELECT COUNT(*) FROM game_identity_resolutions WHERE candidate_content_id='{candidate}';"));
    }

    [Fact]
    public async Task Different_games_can_share_confirmed_canonical_content_id()
    {
        var databasePath = await CreateCurrentDatabaseAsync("shared-canonical.db");
        await InsertGameAsync(databasePath, Game(1), "Game 1");
        await InsertGameAsync(databasePath, Game(2), "Game 2");

        await using var connection = await OpenAsync(databasePath);
        const string canonical =
            "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
        await ExecuteAsync(
            connection,
            $"UPDATE games SET canonical_content_id='{canonical}';");

        Assert.Equal(
            2,
            await ScalarAsync(
                connection,
                $"SELECT COUNT(*) FROM games WHERE canonical_content_id='{canonical}';"));
    }

    [Fact]
    public async Task New_state_with_candidate_is_rejected()
    {
        var databasePath = await CreateCurrentDatabaseAsync("invalid-new.db");
        await InsertGameAsync(databasePath, Game(1), "Game 1");
        await using var connection = await OpenAsync(databasePath);
        await AssertIdentityTableExistsAsync(connection);

        await Assert.ThrowsAsync<SqliteException>(
            () => InsertResolutionAsync(
                connection,
                Game(1),
                null,
                4,
                "cccccccc-cccc-cccc-cccc-cccccccccccc"));
    }

    [Fact]
    public async Task Match_confirmed_state_without_candidate_is_rejected()
    {
        var databasePath = await CreateCurrentDatabaseAsync("invalid-confirmed.db");
        await InsertGameAsync(databasePath, Game(1), "Game 1");
        await using var connection = await OpenAsync(databasePath);
        await AssertIdentityTableExistsAsync(connection);

        await Assert.ThrowsAsync<SqliteException>(
            () => InsertResolutionAsync(
                connection,
                Game(1),
                null,
                1,
                null));
    }

    private async Task<string> CreateCurrentDatabaseAsync(string fileName)
    {
        Directory.CreateDirectory(_root);
        var databasePath = Path.Combine(_root, fileName);
        await new DatabaseInitializer(CreateOptions(databasePath))
            .InitializeAsync(CancellationToken.None);
        return databasePath;
    }

    private DatabaseOptions CreateOptions(string databasePath) =>
        new(
            databasePath,
            Path.Combine(_root, "Backups"));

    private static async Task CreateVersion7DatabaseAsync(
        string databasePath)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(databasePath)!);
        await using var connection = await OpenAsync(databasePath);
        var assembly = typeof(DatabaseInitializer).Assembly;
        var migrations = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(
                "PlayStead.Data.Database.Migrations.",
                StringComparison.Ordinal))
            .Where(name => name.EndsWith(
                ".sql",
                StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Take(7)
            .ToArray();

        Assert.Equal(7, migrations.Length);

        for (var index = 0; index < migrations.Length; index++)
        {
            await using var transaction =
                await connection.BeginTransactionAsync();
            await using var stream =
                assembly.GetManifestResourceStream(migrations[index])!;
            using var reader = new StreamReader(stream);
            var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = await reader.ReadToEndAsync();
            await command.ExecuteNonQueryAsync();
            command.CommandText = "INSERT INTO schema_migrations(version, applied_utc) VALUES ($version, $utc);";
            command.Parameters.AddWithValue("$version", index + 1);
            command.Parameters.AddWithValue("$utc", Utc);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }
    }

    private static async Task InsertGameAsync(
        string databasePath,
        string gameId,
        string title)
    {
        await using var connection = await OpenAsync(databasePath);
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES ($gameId,$title,0,$utc,$utc);";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$utc", Utc);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertResolutionAsync(
        SqliteConnection connection,
        string gameId,
        string? provisionalId,
        int state,
        string? candidateContentId)
    {
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO game_identity_resolutions(game_id,provisional_id,state,candidate_content_id,evidence_json,created_utc,updated_utc) VALUES ($gameId,$provisionalId,$state,$candidateContentId,'{}',$utc,$utc);";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue(
            "$provisionalId",
            (object?)provisionalId ?? DBNull.Value);
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue(
            "$candidateContentId",
            (object?)candidateContentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$utc", Utc);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<SqliteConnection> OpenAsync(
        string databasePath)
    {
        var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False;Foreign Keys=True");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<int> ScalarAsync(
        SqliteConnection connection,
        string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task AssertIdentityTableExistsAsync(
        SqliteConnection connection)
    {
        Assert.Equal(
            1,
            await ScalarAsync(
                connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='game_identity_resolutions';"));
    }

    private static async Task<string?> TextAsync(
        SqliteConnection connection,
        string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static string Game(int index) =>
        $"00000000-0000-4000-8000-{index:000000000000}";

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
