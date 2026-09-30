using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Persistence;
using PlayStead.Data.Catalog;

namespace PlayStead.Data.Tests.Catalog;

public sealed class SqliteCanonicalCatalogStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetMetadata_returns_catalog_schema_and_catalog_version()
    {
        var (options, store) = await CreateStoreAsync();

        var metadata =
            await store.GetMetadataAsync(
                CancellationToken.None);

        Assert.Equal(2, metadata.SchemaVersion);
        Assert.Equal(0L, metadata.CatalogVersion);
        Assert.Equal(DateTimeOffset.UnixEpoch, metadata.GeneratedAtUtc);
        Assert.True(File.Exists(options.CatalogPath));
    }

    [Fact]
    public async Task GetById_and_GetByPublicId_return_canonical_content()
    {
        var (options, store) = await CreateStoreAsync();

        var contentId = new CatalogContentId(
            Guid.Parse(
                "11111111-1111-1111-1111-111111111111"));

        await SeedContentAsync(
            options.CatalogPath,
            contentId,
            "PlayStead-001284",
            "Gray Zone Warfare",
            "gray zone warfare");

        var byId = await store.GetByIdAsync(
            contentId,
            CancellationToken.None);

        var byPublicId = await store.GetByPublicIdAsync(
            PlaySteadPublicId.Parse("PlayStead-001284"),
            CancellationToken.None);

        Assert.NotNull(byId);
        Assert.NotNull(byPublicId);

        Assert.Equal(contentId, byId!.Id);
        Assert.Equal("PlayStead-001284", byId.PublicId.Value);
        Assert.Equal(CatalogContentKind.Game, byId.Kind);
        Assert.Equal("Gray Zone Warfare", byId.CanonicalTitle);
        Assert.Equal("gray zone warfare", byId.NormalizedTitle);
        Assert.Equal(CatalogContentStatus.Active, byId.Status);
        Assert.Null(byId.RedirectTargetId);

        Assert.Equal(byId, byPublicId);
    }

    [Fact]
    public async Task Provider_refs_aliases_and_relations_are_read_with_exact_lookups()
    {
        var (options, store) = await CreateStoreAsync();
        var id = new CatalogContentId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var target = new CatalogContentId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        await SeedContentAsync(options.CatalogPath, id, "PlayStead-001284", "Gray Zone Warfare", "gray zone warfare");
        await SeedContentAsync(options.CatalogPath, target, "PlayStead-001285", "Expansion", "expansion");
        await SeedRelatedDataAsync(options.CatalogPath, id, target);

        var found = await store.FindByProviderRefAsync(CatalogProviderKind.Steam, "1874880", CancellationToken.None);
        var refs = await store.GetProviderRefsAsync(id, CancellationToken.None);
        var aliases = await store.GetAliasesAsync(id, CancellationToken.None);
        var relations = await store.GetRelationsFromAsync(id, CancellationToken.None);

        Assert.Equal(id, found?.Id);
        Assert.Single(refs);
        Assert.Equal("1874880", refs[0].ExternalId);
        Assert.Equal("gray zone", aliases.Single().Alias);
        Assert.Equal(target, relations.Single().TargetContentId);
    }

    [Fact]
    public void Store_contract_is_read_only()
    {
        Assert.True(typeof(ICanonicalCatalogStore).IsAssignableFrom(typeof(SqliteCanonicalCatalogStore)));
        Assert.DoesNotContain(typeof(ICanonicalCatalogStore).GetMethods(), method =>
            method.Name.StartsWith("Insert", StringComparison.Ordinal) ||
            method.Name.StartsWith("Update", StringComparison.Ordinal) ||
            method.Name.StartsWith("Delete", StringComparison.Ordinal) ||
            method.Name.StartsWith("Upsert", StringComparison.Ordinal));
    }

    private async Task<(CatalogDatabaseOptions Options, SqliteCanonicalCatalogStore Store)> CreateStoreAsync()
    {
        Directory.CreateDirectory(_root);

        var options = new CatalogDatabaseOptions(
            Path.Combine(_root, "catalog.db"),
            Path.Combine(_root, "Backups"));

        await new CatalogDatabaseInitializer(options)
            .InitializeAsync(CancellationToken.None);

        return (
            options,
            new SqliteCanonicalCatalogStore(options));
    }

    private static async Task SeedContentAsync(
        string databasePath,
        CatalogContentId contentId,
        string publicId,
        string title,
        string normalizedTitle)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO catalog_contents(
                internal_content_id,
                public_id,
                content_kind,
                canonical_title,
                normalized_title,
                release_date,
                developer,
                publisher,
                status,
                redirect_target_id)
            VALUES(
                $contentId,
                $publicId,
                1,
                $title,
                $normalizedTitle,
                '2024-04-30',
                'MADFINGER Games',
                'MADFINGER Games',
                1,
                NULL);
            """;

        command.Parameters.AddWithValue(
            "$contentId",
            contentId.Value.ToString("D"));
        command.Parameters.AddWithValue("$publicId", publicId);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue(
            "$normalizedTitle",
            normalizedTitle);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedRelatedDataAsync(string databasePath, CatalogContentId id, CatalogContentId target)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO catalog_provider_refs(provider, external_id, content_id, external_type, provenance, confidence, observed_at_utc)
            VALUES (1, '1874880', $id, 'app', 1, 2, '2026-01-01T00:00:00Z');
            INSERT INTO catalog_aliases(content_id, alias, normalized_alias, provenance)
            VALUES ($id, 'gray zone', 'gray zone', 1);
            INSERT INTO catalog_relations(source_content_id, relation_kind, target_content_id, provenance, confidence)
            VALUES ($id, 1, $target, 1, 2);
            """;
        command.Parameters.AddWithValue("$id", id.Value.ToString("D"));
        command.Parameters.AddWithValue("$target", target.Value.ToString("D"));
        await command.ExecuteNonQueryAsync();
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
