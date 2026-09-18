using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Data.Catalog;
using PlayStead.Data.Database;

namespace PlayStead.Data.Tests.Catalog;

public sealed class SqliteCanonicalCatalogWriterTests
{
    [Fact]
    public async Task Exact_steam_import_creates_content_ref_and_link_idempotently()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlaySteadWriter", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var db = Path.Combine(root, "playstead.db");
            var catalog = Path.Combine(root, "catalog.db");
            await new DatabaseInitializer(new DatabaseOptions(db, Path.Combine(root, "backups"))).InitializeAsync(CancellationToken.None);
            await new CatalogDatabaseInitializer(new CatalogDatabaseOptions(catalog, Path.Combine(root, "catalog-backups"))).InitializeAsync(CancellationToken.None);
            var game = GameId.New();
            await using (var connection = new SqliteConnection($"Data Source={db}"))
            { await connection.OpenAsync(); var command = connection.CreateCommand(); command.CommandText = "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES($id,'Enshrouded',0,$now,$now);"; command.Parameters.AddWithValue("$id", game.ToString()); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O")); await command.ExecuteNonQueryAsync(); }
            var writer = new SqliteCanonicalCatalogWriter(new CatalogDatabaseOptions(catalog, ""), new DatabaseOptions(db, ""));
            var item = new CanonicalCatalogImportItem(game, "1203620", "Enshrouded", "Keen Games GmbH", "Keen Games GmbH", DateTimeOffset.UtcNow);
            await writer.ImportSteamAsync([item], CancellationToken.None);
            await writer.ImportSteamAsync([item], CancellationToken.None);
            var store = new SqliteCanonicalCatalogStore(new CatalogDatabaseOptions(catalog, ""));
            var content = await store.FindByProviderRefAsync(CatalogProviderKind.Steam, "1203620", CancellationToken.None);
            Assert.NotNull(content);
            Assert.Equal("Keen Games GmbH", content!.Developer);
            await using (var verify = new SqliteConnection($"Data Source={db};Mode=ReadOnly")) { await verify.OpenAsync(); var check = verify.CreateCommand(); check.CommandText = "SELECT canonical_content_id FROM games WHERE game_id=$id"; check.Parameters.AddWithValue("$id", game.ToString()); Assert.Equal(content.Id.ToString(), Convert.ToString(await check.ExecuteScalarAsync())); }
        }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { } }
    }
}
