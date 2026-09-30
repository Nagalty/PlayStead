using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;

namespace PlayStead.Data.Catalog;

public sealed class CanonicalCatalogBatchImporter
{
    private readonly CatalogDatabaseOptions _options;
    private const int SupportedSchemaVersion = 1;

    public CanonicalCatalogBatchImporter(CatalogDatabaseOptions options) => _options = options;

    public async Task ImportAsync(CanonicalCatalogDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != SupportedSchemaVersion)
            throw new InvalidDataException($"Unsupported catalog schema {document.SchemaVersion}.");
        Validate(document);

        await using var connection = new SqliteConnection($"Data Source={_options.CatalogPath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await ExecuteAsync(connection, transaction, "DELETE FROM catalog_provider_refs; DELETE FROM catalog_aliases; DELETE FROM catalog_relations; DELETE FROM catalog_contents;", cancellationToken);
            foreach (var entry in document.Entries)
            {
                var insert = connection.CreateCommand(); insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO catalog_contents(internal_content_id,public_id,content_kind,canonical_title,normalized_title,release_date,developer,publisher,genres_json,status) VALUES($id,$public,1,$title,$normalized,$release,$developer,$publisher,$genres,1);";
                insert.Parameters.AddWithValue("$id", entry.Id.Value.ToString("D"));
                insert.Parameters.AddWithValue("$public", entry.PublicId.Value);
                insert.Parameters.AddWithValue("$title", entry.CanonicalTitle);
                insert.Parameters.AddWithValue("$normalized", entry.NormalizedTitle);
                insert.Parameters.AddWithValue("$release", entry.ReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty);
                insert.Parameters.AddWithValue("$developer", entry.Developer ?? string.Empty);
                insert.Parameters.AddWithValue("$publisher", entry.Publisher ?? string.Empty);
                insert.Parameters.AddWithValue("$genres", entry.Genres.Count == 0 ? "[]" : JsonSerializer.Serialize(entry.Genres));
                await insert.ExecuteNonQueryAsync(cancellationToken);
                foreach (var reference in entry.ProviderRefs)
                {
                    var referenceCommand = connection.CreateCommand(); referenceCommand.Transaction = transaction;
                    referenceCommand.CommandText = "INSERT INTO catalog_provider_refs(provider,external_id,content_id,external_type,provenance,confidence,observed_at_utc) VALUES($provider,$external,$content,$type,$provenance,$confidence,$observed);";
                    referenceCommand.Parameters.AddWithValue("$provider", (int)reference.Provider);
                    referenceCommand.Parameters.AddWithValue("$external", reference.ExternalId);
                    referenceCommand.Parameters.AddWithValue("$content", entry.Id.Value.ToString("D"));
                    referenceCommand.Parameters.AddWithValue("$type", reference.ExternalType ?? string.Empty);
                    referenceCommand.Parameters.AddWithValue("$provenance", (int)reference.Provenance);
                    referenceCommand.Parameters.AddWithValue("$confidence", (int)reference.Confidence);
                    referenceCommand.Parameters.AddWithValue("$observed", reference.ObservedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                    await referenceCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            var metadata = connection.CreateCommand(); metadata.Transaction = transaction;
            metadata.CommandText = "UPDATE catalog_metadata SET catalog_version=$version, generated_at_utc=$generated WHERE singleton_id=1;";
            metadata.Parameters.AddWithValue("$version", document.CatalogVersion);
            metadata.Parameters.AddWithValue("$generated", document.GeneratedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            await metadata.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void Validate(CanonicalCatalogDocument document)
    {
        if (document.CatalogVersion < 0 || document.Entries.Count > 2_000_000)
            throw new InvalidDataException("Catalog metadata is invalid.");
        var ids = new HashSet<CatalogContentId>();
        var refs = new HashSet<(CatalogProviderKind, string)>();
        foreach (var entry in document.Entries)
        {
            if (!ids.Add(entry.Id) || string.IsNullOrWhiteSpace(entry.CanonicalTitle) || string.IsNullOrWhiteSpace(entry.NormalizedTitle))
                throw new InvalidDataException("Catalog contains an invalid or duplicate entry.");
            foreach (var reference in entry.ProviderRefs)
                if (!Enum.IsDefined(reference.Provider) || string.IsNullOrWhiteSpace(reference.ExternalId) || !refs.Add((reference.Provider, reference.ExternalId)))
                    throw new InvalidDataException("Catalog contains an invalid or duplicate provider reference.");
        }
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken token)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }

}
