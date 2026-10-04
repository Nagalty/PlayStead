using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;

namespace PlayStead.Data.Catalog;

public sealed class SqliteCanonicalCatalogStore : PlayStead.Core.Persistence.ICanonicalCatalogStore
{
    private readonly CatalogDatabaseOptions _options;

    public SqliteCanonicalCatalogStore(
        CatalogDatabaseOptions options)
    {
        _options = options
            ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<CatalogMetadata> GetMetadataAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = CreateReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (
                    SELECT COALESCE(MAX(version), 0)
                    FROM catalog_schema_migrations
                ),
                catalog_version,
                generated_at_utc
            FROM catalog_metadata
            WHERE singleton_id = 1;
            """;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Catalog metadata row is missing.");
        }

        return new CatalogMetadata(
            reader.GetInt32(0),
            reader.GetInt64(1),
            DateTimeOffset.Parse(
                reader.GetString(2),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind));
    }

    public async Task<CatalogContent?> GetByIdAsync(
        CatalogContentId contentId,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            ContentSelect + " WHERE internal_content_id = $id;";

        command.Parameters.AddWithValue(
            "$id",
            contentId.Value.ToString("D"));

        return await ReadSingleContentAsync(
            command,
            cancellationToken);
    }

    public async Task<CatalogContent?> GetByPublicIdAsync(
        PlaySteadPublicId publicId,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            ContentSelect + " WHERE public_id = $publicId;";

        command.Parameters.AddWithValue(
            "$publicId",
            publicId.Value);

        return await ReadSingleContentAsync(
            command,
            cancellationToken);
    }

    public async Task<CatalogContent?> FindByProviderRefAsync(
        CatalogProviderKind provider, string externalId, CancellationToken cancellationToken)
    {
        await using var connection = CreateReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = ContentSelect + " INNER JOIN catalog_provider_refs r ON r.content_id = internal_content_id WHERE r.provider = $provider AND r.external_id = $externalId;";
        command.Parameters.AddWithValue("$provider", (int)provider);
        command.Parameters.AddWithValue("$externalId", externalId);
        return await ReadSingleContentAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogContent>> FindByNormalizedTitleAsync(
        string normalizedTitle,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedTitle);
        await using var connection = CreateReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = ContentSelect + " WHERE normalized_title = $normalizedTitle ORDER BY internal_content_id;";
        command.Parameters.AddWithValue("$normalizedTitle", normalizedTitle);
        var results = new List<CatalogContent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            results.Add(ReadContent(reader));
        return results;
    }

    public async Task<IReadOnlyList<CatalogProviderRef>> GetProviderRefsAsync(
        CatalogContentId contentId, CancellationToken cancellationToken)
    {
        await using var connection = CreateReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT provider, external_id, external_type, provenance, confidence, observed_at_utc FROM catalog_provider_refs WHERE content_id = $id ORDER BY provider, external_id;";
        command.Parameters.AddWithValue("$id", contentId.Value.ToString("D"));
        var result = new List<CatalogProviderRef>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new CatalogProviderRef(contentId, (CatalogProviderKind)reader.GetInt32(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), (CatalogProvenance)reader.GetInt32(3), (CatalogConfidence)reader.GetInt32(4), DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        return result;
    }

    public async Task<IReadOnlyList<CatalogAlias>> GetAliasesAsync(
        CatalogContentId contentId, CancellationToken cancellationToken)
    {
        await using var connection = CreateReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT alias, normalized_alias, provenance FROM catalog_aliases WHERE content_id = $id ORDER BY alias;";
        command.Parameters.AddWithValue("$id", contentId.Value.ToString("D"));
        var result = new List<CatalogAlias>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new CatalogAlias(contentId, reader.GetString(0), reader.GetString(1), (CatalogProvenance)reader.GetInt32(2)));
        return result;
    }

    public async Task<IReadOnlyList<CatalogContentRelation>> GetRelationsFromAsync(
        CatalogContentId sourceContentId, CancellationToken cancellationToken)
    {
        await using var connection = CreateReadOnlyConnection();
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT relation_kind, target_content_id, provenance, confidence FROM catalog_relations WHERE source_content_id = $id ORDER BY relation_kind, target_content_id;";
        command.Parameters.AddWithValue("$id", sourceContentId.Value.ToString("D"));
        var result = new List<CatalogContentRelation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new CatalogContentRelation(sourceContentId, (CatalogRelationKind)reader.GetInt32(0), new CatalogContentId(Guid.Parse(reader.GetString(1))), (CatalogProvenance)reader.GetInt32(2), (CatalogConfidence)reader.GetInt32(3)));
        return result;
    }

    private SqliteConnection CreateReadOnlyConnection() =>
        new(
            $"Data Source={_options.CatalogPath};Mode=ReadOnly;Pooling=False");

    private static async Task<CatalogContent?> ReadSingleContentAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadContent(reader);
    }

    private static CatalogContent ReadContent(SqliteDataReader reader) => new(
            new CatalogContentId(
                Guid.Parse(reader.GetString(0))),
            PlaySteadPublicId.Parse(
                reader.GetString(1)),
            (CatalogContentKind)reader.GetInt32(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) || string.IsNullOrEmpty(reader.GetString(5))
                ? null
                : DateOnly.ParseExact(
                    reader.GetString(5),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture),
            reader.IsDBNull(6) || string.IsNullOrEmpty(reader.GetString(6))
                ? null
                : reader.GetString(6),
            reader.IsDBNull(7) || string.IsNullOrEmpty(reader.GetString(7))
                ? null
                : reader.GetString(7),
            (CatalogContentStatus)reader.GetInt32(8),
            reader.IsDBNull(9)
                ? null
                : new CatalogContentId(
            Guid.Parse(reader.GetString(9))),
            reader.IsDBNull(10)
                ? null
                : JsonSerializer.Deserialize<string[]>(reader.GetString(10)),
            reader.IsDBNull(11) && reader.IsDBNull(12)
                ? null
                : new CatalogMedia(
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    reader.IsDBNull(13) ? null : reader.GetInt32(13),
                    reader.IsDBNull(14) ? null : reader.GetInt32(14),
                    reader.IsDBNull(15) ? null : reader.GetInt32(15),
                    reader.IsDBNull(16) ? null : reader.GetInt32(16),
                    reader.IsDBNull(17) ? null : reader.GetString(17)));

    private const string ContentSelect = """
        SELECT
            internal_content_id,
            public_id,
            content_kind,
            canonical_title,
            normalized_title,
            release_date,
            developer,
            publisher,
            status,
            redirect_target_id,
            genres_json,
            cover_url,
            hero_url,
            cover_width,
            cover_height,
            hero_width,
            hero_height,
            media_source
        FROM catalog_contents
        """;
}
