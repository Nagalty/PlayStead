using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
using PlayStead.Core.Library;
using PlayStead.Core.Media;
using PlayStead.Core.Persistence;
using PlayStead.Data.Database;
using PlayStead.Data.ManualMetadata;

namespace PlayStead.Data.Tests.ManualMetadata;

public sealed class SqliteManualMetadataLinkStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlaySteadManualLinks", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Link_round_trips_and_survives_new_store_instance()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var game = GameId.New();
        var link = new ManualMetadataLink(game, CatalogContentId.New(), new MediaSourceIdentity(ProviderKind.Steam, "123"), DateTimeOffset.UtcNow);
        await new SqliteManualMetadataLinkStore(options).UpsertAsync(link, CancellationToken.None);

        var loaded = await new SqliteManualMetadataLinkStore(options).GetAsync(game, CancellationToken.None);

        Assert.Equal(link.CanonicalCatalogId, loaded?.CanonicalCatalogId);
        Assert.Equal(ProviderKind.Steam, loaded?.MediaSource?.Provider);
        Assert.Equal("123", loaded?.MediaSource?.ExternalId);
    }

    [Fact]
    public async Task Removing_link_removes_media_association()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, "playstead.db"), Path.Combine(_root, "backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        var game = GameId.New();
        await new SqliteManualMetadataLinkStore(options).UpsertAsync(new(game, CatalogContentId.New(), new(ProviderKind.Steam, "123"), DateTimeOffset.UtcNow), CancellationToken.None);
        var store = new SqliteManualMetadataLinkStore(options);
        await store.RemoveAsync(game, CancellationToken.None);
        Assert.Null(await store.GetAsync(game, CancellationToken.None));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
