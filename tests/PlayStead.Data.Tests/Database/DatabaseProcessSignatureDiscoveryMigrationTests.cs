using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;

namespace PlayStead.Data.Tests.Database;

public sealed class DatabaseProcessSignatureDiscoveryMigrationTests
{
    private static readonly string Utc = "2026-09-12T00:00:00.0000000+00:00";

    [Fact]
    public async Task Fresh_database_has_schema_6_and_discovery_constraints()
    {
        using var fixture = new DiscoveryDatabaseFixture();
        await fixture.InitializeAsync(CancellationToken.None);
        await using var connection = await fixture.OpenAsync(CancellationToken.None);
        Assert.Equal(25, await ScalarAsync(connection, "SELECT MAX(version) FROM schema_migrations;"));
        Assert.Equal(3, await ScalarAsync(connection, "SELECT COUNT(*) FROM pragma_table_info('process_signature_entries') WHERE name IN ('executable_path','validated_size_bytes','validated_last_write_utc');"));
        Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'process_signature_validation';"));
        Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'process_signature_learning';"));
        var game = GameId.New();
        var installation = InstallationId.New();
        await fixture.SeedInstallationAsync(game, installation, "C:/fixture", CancellationToken.None);
        await ExecuteAsync(connection, $"INSERT INTO process_signatures VALUES ('{game}',0,'{Utc}');");
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(connection, $"INSERT INTO process_signature_entries(game_id,ordinal,executable_name,kind,validated_size_bytes) VALUES ('{game}',0,'a.exe',0,-1);"));
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(connection, $"INSERT INTO process_signature_validation(game_id,validation_state,concurrency_token) VALUES ('{game}',3,'12345678901234567890123456789012');"));
    }

    [Fact]
    public async Task V5_upgrade_preserves_all_signature_origins_and_entry_order()
    {
        using var fixture = new DiscoveryDatabaseFixture();
        await DiscoveryDatabaseFixture.CreateSchemaAsync(fixture.Options.DatabasePath, 5, CancellationToken.None);
        await using (var connection = await fixture.OpenAsync(CancellationToken.None))
        {
            await SeedSignaturesAsync(connection);
            var before = await RowsAsync(connection, "SELECT s.game_id,s.origin,s.updated_at_utc,e.ordinal,e.executable_name,e.kind FROM process_signatures s JOIN process_signature_entries e USING(game_id) ORDER BY s.game_id,e.ordinal;");
            Assert.Equal(9, before.Length);
            await connection.CloseAsync();
            await fixture.InitializeAsync(CancellationToken.None);
            await using var upgraded = await fixture.OpenAsync(CancellationToken.None);
            var after = await RowsAsync(upgraded, "SELECT s.game_id,s.origin,s.updated_at_utc,e.ordinal,e.executable_name,e.kind FROM process_signatures s JOIN process_signature_entries e USING(game_id) ORDER BY s.game_id,e.ordinal;");
            Assert.Equal(before, after);
            Assert.Equal(25, await ScalarAsync(upgraded, "SELECT MAX(version) FROM schema_migrations;"));
            Assert.Equal(1, await ScalarAsync(upgraded, "SELECT COUNT(*) FROM process_signature_validation;"));
        }
    }

    [Fact]
    public async Task V5_upgrade_preserves_sessions_and_traceable_corrections()
    {
        using var fixture = new DiscoveryDatabaseFixture();
        await DiscoveryDatabaseFixture.CreateSchemaAsync(fixture.Options.DatabasePath, 5, CancellationToken.None);
        await using (var connection = await fixture.OpenAsync(CancellationToken.None))
        {
            await SeedSignaturesAsync(connection);
            for (var i = 0; i < 4; i++)
            {
                var session = Guid.NewGuid().ToString("D");
                var game = i % 2 == 0 ? Game(0) : Game(1);
                await ExecuteAsync(connection, $"INSERT INTO game_sessions VALUES ('{session}','{game}','{Utc}','{Utc}',{(i < 2 ? "NULL" : $"'{Utc}'")},{(i < 2 ? 0 : 1)},{(i < 2 ? "NULL" : "0")},0,'{Utc}','{Utc}');");
                if (i >= 2)
                    await ExecuteAsync(connection, $"INSERT INTO session_corrections VALUES ('{Guid.NewGuid():D}','{session}',{(i == 2 ? $"'{Utc}'" : "NULL")},'{Utc}',NULL,'{Utc}');");
            }
            var sessionsBefore = await RowsAsync(connection, "SELECT * FROM game_sessions ORDER BY session_id;");
            var correctionsBefore = await RowsAsync(connection, "SELECT * FROM session_corrections ORDER BY session_id;");
            Assert.Equal(4, sessionsBefore.Length);
            Assert.Equal(2, correctionsBefore.Length);
            await connection.CloseAsync();
            await fixture.InitializeAsync(CancellationToken.None);
            await using var upgraded = await fixture.OpenAsync(CancellationToken.None);
            Assert.Equal(sessionsBefore, await RowsAsync(upgraded, "SELECT * FROM game_sessions ORDER BY session_id;"));
            Assert.Equal(correctionsBefore, await RowsAsync(upgraded, "SELECT * FROM session_corrections ORDER BY session_id;"));
        }
    }

    [Fact]
    public async Task Legacy_discovered_is_unvalidated_without_invented_path()
    {
        using var fixture = await UpgradedFixtureAsync();
        await using var connection = await fixture.OpenAsync(CancellationToken.None);
        Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM process_signature_validation WHERE validation_state = 0 AND installation_id IS NULL AND generation_id IS NULL AND policy_version IS NULL AND length(concurrency_token)=32;"));
        Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM process_signature_entries WHERE executable_path IS NOT NULL OR validated_size_bytes IS NOT NULL OR validated_last_write_utc IS NOT NULL;"));
    }

    [Fact]
    public async Task Explicit_legacy_signatures_have_no_discovery_metadata()
    {
        using var fixture = await UpgradedFixtureAsync();
        await using var connection = await fixture.OpenAsync(CancellationToken.None);
        Assert.Equal(0, await ScalarAsync(connection, $"SELECT COUNT(*) FROM process_signature_validation WHERE game_id = '{Game(1)}';"));
        Assert.Equal(0, await ScalarAsync(connection, $"SELECT COUNT(*) FROM process_signature_validation WHERE game_id = '{Game(2)}';"));
    }

    [Fact]
    public async Task Reinitialization_does_not_repeat_migration_or_backup()
    {
        using var fixture = await UpgradedFixtureAsync();
        var before = Directory.GetFiles(fixture.Options.BackupsDirectory);
        await fixture.InitializeAsync(CancellationToken.None);
        await using var connection = await fixture.OpenAsync(CancellationToken.None);
        Assert.Equal(25, await ScalarAsync(connection, "SELECT MAX(version) FROM schema_migrations;"));
        Assert.Equal(before, Directory.GetFiles(fixture.Options.BackupsDirectory));
    }

    [Fact]
    public async Task Intermediate_failure_rolls_back_006_and_restores_original_file()
    {
        using var fixture = new DiscoveryDatabaseFixture();
        await DiscoveryDatabaseFixture.CreateSchemaAsync(fixture.Options.DatabasePath, 5, CancellationToken.None);
        await using (var connection = await fixture.OpenAsync(CancellationToken.None))
        {
            await ExecuteAsync(connection, $"INSERT INTO games VALUES ('{Game(0)}','Game',0,'{Utc}','{Utc}'); INSERT INTO process_signatures VALUES ('{Game(0)}',0,'{Utc}');");
            await ExecuteAsync(connection, "CREATE TABLE process_signature_learning (collision INTEGER);");
            await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(connection, "CREATE TABLE process_signature_learning (second INTEGER);"));
            Assert.Equal(1, await ScalarAsync(connection, "SELECT COUNT(*) FROM sqlite_master WHERE name='process_signature_learning';"));
            Assert.Equal(5, await ScalarAsync(connection, "SELECT MAX(version) FROM schema_migrations;"));
        }
        var original = await File.ReadAllBytesAsync(fixture.Options.DatabasePath);
        await using (var direct = await fixture.OpenAsync(CancellationToken.None))
        {
            await using var directTx = await direct.BeginTransactionAsync();
            using var directCommand = direct.CreateCommand();
            directCommand.Transaction = (SqliteTransaction)directTx;
            directCommand.CommandText = await Read006Async();
            var directFailure = await Record.ExceptionAsync(async () =>
            {
                await using var result = await directCommand.ExecuteReaderAsync();
                while (await result.NextResultAsync()) { }
            });
            await directTx.RollbackAsync();
            Assert.IsType<SqliteException>(directFailure);
        }
        await using (var fresh = await fixture.OpenAsync(CancellationToken.None))
        {
            Assert.Equal(5, await ScalarAsync(fresh, "SELECT MAX(version) FROM schema_migrations;"));
            Assert.Equal(0, await ScalarAsync(fresh, "SELECT COUNT(*) FROM pragma_table_info('process_signature_entries') WHERE name='executable_path';"));
        }
        await Assert.ThrowsAsync<SqliteException>(() => fixture.InitializeAsync(CancellationToken.None));
        Assert.Equal(original, await File.ReadAllBytesAsync(fixture.Options.DatabasePath));
        var backup = Assert.Single(Directory.GetFiles(fixture.Options.BackupsDirectory, "*.bak"));
        Assert.Equal(original, await File.ReadAllBytesAsync(backup));
        await using (var connection = await fixture.OpenAsync(CancellationToken.None))
        {
            Assert.Equal(5, await ScalarAsync(connection, "SELECT MAX(version) FROM schema_migrations;"));
            Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM pragma_table_info('process_signature_entries') WHERE name='executable_path';"));
        }
        await using var reopened = await fixture.OpenAsync(CancellationToken.None);
        Assert.Equal(0, await ScalarAsync(reopened, "SELECT COUNT(*) FROM pragma_table_info('process_signature_entries') WHERE name='executable_path';"));
        Assert.Equal(5, await ScalarAsync(reopened, "SELECT MAX(version) FROM schema_migrations;"));
    }

    [Fact]
    public async Task Installation_delete_cascades_learning_and_suspends_discovered()
    {
        using var fixture = await UpgradedFixtureAsync();
        var game = Game(0);
        var installation = InstallationId.New();
        await fixture.SeedInstallationAsync(new GameId(Guid.Parse(game)), installation, "C:/fixture", CancellationToken.None);
        await using var connection = await fixture.OpenAsync(CancellationToken.None);
        await ExecuteAsync(connection, $"UPDATE process_signature_validation SET installation_id='{installation}',validation_state=1,concurrency_token='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' WHERE game_id='{game}';");
        await ExecuteAsync(connection, $"INSERT INTO process_signature_learning(installation_id,game_id,root_path,generation_id,policy_version,concurrency_token,has_ambiguous_installation,inventory_json,reasons_json) VALUES ('{installation}','{game}','C:/fixture','{Guid.NewGuid():D}',1,'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',0,'[]','[]');");
        await ExecuteAsync(connection, $"DELETE FROM installations WHERE installation_id='{installation}';");
        Assert.Equal(0, await ScalarAsync(connection, "SELECT COUNT(*) FROM process_signature_learning;"));
        Assert.Equal(1, await ScalarAsync(connection, $"SELECT COUNT(*) FROM process_signature_validation WHERE game_id='{game}' AND installation_id IS NULL AND validation_state=0 AND concurrency_token <> 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';"));
        Assert.Equal(1, await ScalarAsync(connection, $"SELECT COUNT(*) FROM process_signatures WHERE game_id='{game}';"));
    }

    [Fact]
    public async Task Game_delete_cascades_new_and_existing_dependents()
    {
        using var fixture = await UpgradedFixtureAsync();
        var game = Game(0);
        var installation = InstallationId.New();
        await fixture.SeedInstallationAsync(new GameId(Guid.Parse(game)), installation, "C:/fixture", CancellationToken.None);
        await using var connection = await fixture.OpenAsync(CancellationToken.None);
        var session = Guid.NewGuid().ToString("D");
        await ExecuteAsync(connection, $"INSERT INTO process_signature_learning(installation_id,game_id,root_path,generation_id,policy_version,concurrency_token,has_ambiguous_installation,inventory_json,reasons_json) VALUES ('{installation}','{game}','C:/fixture','{Guid.NewGuid():D}',1,'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',0,'[]','[]'); INSERT INTO game_sessions VALUES ('{session}','{game}','{Utc}','{Utc}',NULL,0,NULL,0,'{Utc}','{Utc}'); INSERT INTO session_corrections VALUES ('{Guid.NewGuid():D}','{session}','{Utc}',NULL,NULL,'{Utc}');");
        await ExecuteAsync(connection, $"DELETE FROM games WHERE game_id='{game}';");
        foreach (var table in new[] { "process_signatures", "process_signature_entries", "process_signature_validation", "process_signature_learning", "game_sessions" })
            Assert.Equal(0, await ScalarAsync(connection, $"SELECT COUNT(*) FROM {table} WHERE game_id='{game}';"));
        Assert.Equal(0, await ScalarAsync(connection, $"SELECT COUNT(*) FROM session_corrections WHERE session_id='{session}';"));
    }

    [Fact]
    public async Task Learning_row_cannot_reference_another_games_installation()
    {
        using var fixture = await UpgradedFixtureAsync();
        var other = GameId.New();
        var installation = InstallationId.New();
        await fixture.SeedInstallationAsync(other, installation, "C:/other", CancellationToken.None);
        await using var connection = await fixture.OpenAsync(CancellationToken.None);
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(connection, $"INSERT INTO process_signature_learning(installation_id,game_id,root_path,generation_id,policy_version,concurrency_token,has_ambiguous_installation,inventory_json,reasons_json) VALUES ('{installation}','{Game(0)}','C:/other','{Guid.NewGuid():D}',1,'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',0,'[]','[]');"));
    }

    private static string Game(int index) => $"00000000-0000-4000-8000-{index + 1:000000000000}";

    private static async Task<DiscoveryDatabaseFixture> UpgradedFixtureAsync()
    {
        var fixture = new DiscoveryDatabaseFixture();
        await DiscoveryDatabaseFixture.CreateSchemaAsync(fixture.Options.DatabasePath, 5, CancellationToken.None);
        await using (var connection = await fixture.OpenAsync(CancellationToken.None))
            await SeedSignaturesAsync(connection);
        await fixture.InitializeAsync(CancellationToken.None);
        return fixture;
    }

    private static async Task SeedSignaturesAsync(SqliteConnection connection)
    {
        for (var i = 0; i < 3; i++)
        {
            var game = Game(i);
            await ExecuteAsync(connection, $"INSERT INTO games VALUES ('{game}','Game {i}',0,'{Utc}','{Utc}'); INSERT INTO process_signatures VALUES ('{game}',{i},'{Utc}');");
            for (var ordinal = 0; ordinal < 3; ordinal++)
                await ExecuteAsync(connection, $"INSERT INTO process_signature_entries VALUES ('{game}',{ordinal},'game{i}-{2 - ordinal}.exe',{ordinal});");
        }
    }

    private static async Task<int> ScalarAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string[]> RowsAsync(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var result = new List<string>();
        while (await reader.ReadAsync())
            result.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "<null>" : Convert.ToString(reader.GetValue(i))!)));
        return result.ToArray();
    }

    private static async Task<string> Read006Async()
    {
        var assembly = typeof(PlayStead.Data.Database.DatabaseInitializer).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("006_process_signature_discovery.sql", StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
