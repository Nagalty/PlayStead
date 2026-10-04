using PlayStead.Core.Catalog;
using PlayStead.Data.Catalog;

namespace PlayStead.Data.Tests.Catalog;

public sealed class CanonicalCatalogBatchImporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Imports_entries_provider_refs_and_genres_atomically()
    {
        Directory.CreateDirectory(_root);
        var options = new CatalogDatabaseOptions(Path.Combine(_root, "catalog.db"), Path.Combine(_root, "Backups"));
        await new CatalogDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var id = new CatalogContentId(Guid.NewGuid());
        var document = new CanonicalCatalogDocument(1, 42, DateTimeOffset.UtcNow,
            [new CanonicalCatalogEntry(id, PlaySteadPublicId.Parse("PlayStead-000042"), "Dune: Awakening", "DUNE AWAKENING", null, "Funcom", "Funcom", ["RPG"],
                [new CanonicalCatalogProviderReference(CatalogProviderKind.Steam, "1172710", "app", CatalogProvenance.Igdb, CatalogConfidence.Deterministic, DateTimeOffset.UtcNow)], CatalogProvenance.Igdb, DateTimeOffset.UtcNow,
                new CatalogMedia("https://images.igdb.com/igdb/image/upload/t_600x900/cover.jpg", "https://images.igdb.com/igdb/image/upload/t_1920x1080/hero.jpg", 600, 900, 1920, 1080, "igdb"))]);

        await new CanonicalCatalogBatchImporter(options).ImportAsync(document, CancellationToken.None);
        var store = new SqliteCanonicalCatalogStore(options);
        var content = await store.GetByIdAsync(id, CancellationToken.None);
        Assert.Equal("Dune: Awakening", content?.CanonicalTitle);
        Assert.Equal(["RPG"], content?.Genres);
        Assert.Equal("https://images.igdb.com/igdb/image/upload/t_600x900/cover.jpg", content?.Media?.CoverUrl);
        Assert.Equal(1920, content?.Media?.HeroWidth);
        Assert.Equal(id, (await store.FindByProviderRefAsync(CatalogProviderKind.Steam, "1172710", CancellationToken.None))?.Id);
        Assert.Equal(42, (await store.GetMetadataAsync(CancellationToken.None)).CatalogVersion);
    }

    [Fact]
    public async Task Rejects_non_https_or_loopback_media_urls()
    {
        Directory.CreateDirectory(_root);
        var options = new CatalogDatabaseOptions(Path.Combine(_root, "catalog.db"), Path.Combine(_root, "Backups"));
        await new CatalogDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var entry = Entry(Guid.NewGuid(), new CanonicalCatalogProviderReference(CatalogProviderKind.Epic, "epic", null, CatalogProvenance.Igdb, CatalogConfidence.Deterministic, DateTimeOffset.UtcNow), "PlayStead-000043") with
        {
            Media = new CatalogMedia("http://127.0.0.1/cover.jpg", null)
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => new CanonicalCatalogBatchImporter(options).ImportAsync(new CanonicalCatalogDocument(1, 2, DateTimeOffset.UtcNow, [entry]), CancellationToken.None));
    }

    [Fact]
    public async Task Imports_legacy_entry_without_media_as_null()
    {
        Directory.CreateDirectory(_root);
        var options = new CatalogDatabaseOptions(Path.Combine(_root, "catalog.db"), Path.Combine(_root, "Backups"));
        await new CatalogDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var id = new CatalogContentId(Guid.NewGuid());
        await new CanonicalCatalogBatchImporter(options).ImportAsync(
            new CanonicalCatalogDocument(1, 3, DateTimeOffset.UtcNow, [Entry(id.Value, null, "PlayStead-000045")]),
            CancellationToken.None);
        Assert.Null((await new SqliteCanonicalCatalogStore(options).GetByIdAsync(id, CancellationToken.None))?.Media);
    }

    [Fact]
    public async Task Invalid_duplicate_provider_reference_is_rejected_without_replacing_catalog()
    {
        Directory.CreateDirectory(_root);
        var options = new CatalogDatabaseOptions(Path.Combine(_root, "catalog.db"), Path.Combine(_root, "Backups"));
        await new CatalogDatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var duplicate = new CanonicalCatalogProviderReference(CatalogProviderKind.Steam, "1", null, CatalogProvenance.Igdb, CatalogConfidence.Deterministic, DateTimeOffset.UtcNow);
        var document = new CanonicalCatalogDocument(1, 1, DateTimeOffset.UtcNow,
            [Entry(Guid.NewGuid(), duplicate, "PlayStead-000043"), Entry(Guid.NewGuid(), duplicate, "PlayStead-000044")]);
        await Assert.ThrowsAsync<InvalidDataException>(() => new CanonicalCatalogBatchImporter(options).ImportAsync(document, CancellationToken.None));
        Assert.Equal(0, (await new SqliteCanonicalCatalogStore(options).GetMetadataAsync(CancellationToken.None)).CatalogVersion);
    }

    private static CanonicalCatalogEntry Entry(Guid id, CanonicalCatalogProviderReference? reference, string publicId) =>
        new(new CatalogContentId(id), PlaySteadPublicId.Parse(publicId), "Game", "GAME", null, null, null, [], reference is null ? [] : [reference], CatalogProvenance.Igdb, DateTimeOffset.UtcNow);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
