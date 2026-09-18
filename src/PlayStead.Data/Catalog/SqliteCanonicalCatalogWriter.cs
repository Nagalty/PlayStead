using System.Globalization;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Persistence;
using PlayStead.Data.Database;

namespace PlayStead.Data.Catalog;

public sealed class SqliteCanonicalCatalogWriter : ICanonicalCatalogWriter
{
    private readonly CatalogDatabaseOptions _catalog;
    private readonly DatabaseOptions _library;

    public SqliteCanonicalCatalogWriter(CatalogDatabaseOptions catalog, DatabaseOptions library)
    { _catalog = catalog; _library = library; }

    public async Task ImportSteamAsync(IReadOnlyCollection<CanonicalCatalogImportItem> items, CancellationToken cancellationToken)
    {
        await using var catalog = new SqliteConnection($"Data Source={_catalog.CatalogPath};Pooling=False");
        await catalog.OpenAsync(cancellationToken);
        await using var library = new SqliteConnection($"Data Source={_library.DatabasePath};Pooling=False");
        await library.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await catalog.BeginTransactionAsync(cancellationToken);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var contentId = await FindContentAsync(catalog, transaction, item.ExternalId, cancellationToken);
            if (contentId is null)
            {
                contentId = CatalogContentId.New();
                var insert = catalog.CreateCommand(); insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO catalog_contents(internal_content_id,public_id,content_kind,canonical_title,normalized_title,release_date,developer,publisher,status) VALUES($id,$public,1,$title,$normalized,NULL,$developer,$publisher,1);";
                insert.Parameters.AddWithValue("$id", contentId.Value.ToString());
                insert.Parameters.AddWithValue("$public", $"PlayStead-{item.ExternalId}");
                insert.Parameters.AddWithValue("$title", item.Name);
                insert.Parameters.AddWithValue("$normalized", item.Name.Trim().ToUpperInvariant());
                insert.Parameters.AddWithValue("$developer", (object?)item.Developer ?? DBNull.Value);
                insert.Parameters.AddWithValue("$publisher", (object?)item.Publisher ?? DBNull.Value);
                await insert.ExecuteNonQueryAsync(cancellationToken);
                var reference = catalog.CreateCommand(); reference.Transaction = transaction;
                reference.CommandText = "INSERT INTO catalog_provider_refs(provider,external_id,content_id,external_type,provenance,confidence,observed_at_utc) VALUES(1,$external,$id,NULL,1,1,$observed);";
                reference.Parameters.AddWithValue("$external", item.ExternalId); reference.Parameters.AddWithValue("$id", contentId.Value.ToString()); reference.Parameters.AddWithValue("$observed", item.ObservedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                await reference.ExecuteNonQueryAsync(cancellationToken);
            }
            var link = library.CreateCommand();
            link.CommandText = "UPDATE games SET canonical_content_id=$content WHERE game_id=$game AND (canonical_content_id IS NULL OR canonical_content_id=$content);";
            link.Parameters.AddWithValue("$content", contentId.Value.ToString()); link.Parameters.AddWithValue("$game", item.GameId.ToString());
            await link.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<CatalogContentId?> FindContentAsync(SqliteConnection connection, SqliteTransaction tx, string externalId, CancellationToken token)
    {
        var command = connection.CreateCommand(); command.Transaction = tx; command.CommandText = "SELECT content_id FROM catalog_provider_refs WHERE provider=1 AND external_id=$external;"; command.Parameters.AddWithValue("$external", externalId);
        var value = await command.ExecuteScalarAsync(token); return value is string text && Guid.TryParse(text, out var id) ? new CatalogContentId(id) : null;
    }
}
