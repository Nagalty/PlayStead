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
                [new CanonicalCatalogProviderReference(CatalogProviderKind.Steam, "1172710", "app", CatalogProvenance.Igdb, CatalogConfidence.Deterministic, DateTimeOffset.UtcNow)], CatalogProvenance.Igdb, DateTimeOffset.UtcNow)]);

        await new CanonicalCatalogBatchImporter(options).ImportAsync(document, CancellationToken.None);
        var store = new SqliteCanonicalCatalogStore(options);
        var content = await store.GetByIdAsync(id, CancellationToken.None);
        Assert.Equal("Dune: Awakening", content?.CanonicalTitle);
        Assert.Equal(["RPG"], content?.Genres);
        Assert.Equal(id, (await store.FindByProviderRefAsync(CatalogProviderKind.Steam, "1172710", CancellationToken.None))?.Id);
        Assert.Equal(42, (await store.GetMetadataAsync(CancellationToken.None)).CatalogVersion);
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

    private static CanonicalCatalogEntry Entry(Guid id, CanonicalCatalogProviderReference reference, string publicId) =>
        new(new CatalogContentId(id), PlaySteadPublicId.Parse(publicId), "Game", "GAME", null, null, null, [], [reference], CatalogProvenance.Igdb, DateTimeOffset.UtcNow);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
