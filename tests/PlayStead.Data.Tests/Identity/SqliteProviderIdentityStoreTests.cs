using PlayStead.Core.Catalog;
using PlayStead.Core.Identity;
using PlayStead.Core.Library;
using PlayStead.Data.Database;
using PlayStead.Data.Identity;

namespace PlayStead.Data.Tests.Identity;

public sealed class SqliteProviderIdentityStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        "ProviderIdentityStore",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Persists_multiple_provider_identities_for_one_game()
    {
        var (options, gameId) = await CreateDatabaseAsync();
        var sut = new SqliteProviderIdentityStore(options);
        var now = DateTimeOffset.UtcNow;

        await sut.AssociateAsync(new(gameId, ProviderKind.Steam, "123", ProviderIdentitySource.CanonicalCatalog, CatalogConfidence.Deterministic, now, now), CancellationToken.None);
        await sut.AssociateAsync(new(gameId, ProviderKind.Gog, "456", ProviderIdentitySource.UserConfirmed, CatalogConfidence.Deterministic, now, now), CancellationToken.None);

        var identities = await sut.GetByGameIdAsync(gameId, CancellationToken.None);

        Assert.Collection(
            identities,
            steam => Assert.Equal((ProviderKind.Steam, "123", ProviderIdentitySource.CanonicalCatalog), (steam.Provider, steam.ExternalId, steam.Source)),
            gog => Assert.Equal((ProviderKind.Gog, "456", ProviderIdentitySource.UserConfirmed), (gog.Provider, gog.ExternalId, gog.Source)));
    }

    [Fact]
    public async Task Rejects_provider_identity_already_owned_by_another_game()
    {
        var (options, firstGame) = await CreateDatabaseAsync();
        var secondGame = await InsertGameAsync(options, "Second");
        var sut = new SqliteProviderIdentityStore(options);
        var now = DateTimeOffset.UtcNow;

        await sut.AssociateAsync(new(firstGame, ProviderKind.Steam, "123", ProviderIdentitySource.UserConfirmed, CatalogConfidence.Deterministic, now, now), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AssociateAsync(
            new(secondGame, ProviderKind.Steam, "123", ProviderIdentitySource.UserConfirmed, CatalogConfidence.Deterministic, now, now),
            CancellationToken.None));
    }

    [Fact]
    public async Task Catalog_link_does_not_downgrade_user_confirmed_source()
    {
        var (options, gameId) = await CreateDatabaseAsync();
        var sut = new SqliteProviderIdentityStore(options);
        var now = DateTimeOffset.UtcNow;

        await sut.AssociateAsync(new(gameId, ProviderKind.Steam, "123", ProviderIdentitySource.UserConfirmed, CatalogConfidence.Deterministic, now, now), CancellationToken.None);
        await sut.AssociateAsync(new(gameId, ProviderKind.Steam, "123", ProviderIdentitySource.CanonicalCatalog, CatalogConfidence.Deterministic, now.AddMinutes(1), now.AddMinutes(1)), CancellationToken.None);

        var identity = Assert.Single(await sut.GetByGameIdAsync(gameId, CancellationToken.None));
        Assert.Equal(ProviderIdentitySource.UserConfirmed, identity.Source);
    }

    [Fact]
    public async Task Removing_association_is_reversible_without_deleting_the_game()
    {
        var (options, gameId) = await CreateDatabaseAsync();
        var sut = new SqliteProviderIdentityStore(options);
        var now = DateTimeOffset.UtcNow;
        await sut.AssociateAsync(new(gameId, ProviderKind.Gog, "456", ProviderIdentitySource.UserConfirmed, CatalogConfidence.Deterministic, now, now), CancellationToken.None);

        Assert.True(await sut.RemoveAsync(gameId, ProviderKind.Gog, "456", CancellationToken.None));
        Assert.Empty(await sut.GetByGameIdAsync(gameId, CancellationToken.None));
        Assert.False(await sut.RemoveAsync(gameId, ProviderKind.Gog, "456", CancellationToken.None));
    }

    [Fact]
    public async Task Existing_scan_reference_is_exposed_as_provider_observation()
    {
        var (options, gameId) = await CreateDatabaseAsync();
        await InsertReferenceAsync(options, gameId, ProviderKind.Gog, "1103900211");
        var sut = new SqliteProviderIdentityStore(options);

        var identity = Assert.Single(await sut.GetByGameIdAsync(gameId, CancellationToken.None));

        Assert.Equal(ProviderIdentitySource.ProviderObservation, identity.Source);
        Assert.Equal(CatalogConfidence.Deterministic, identity.Confidence);
    }

    private async Task<(DatabaseOptions Options, GameId GameId)> CreateDatabaseAsync()
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(Path.Combine(_root, Guid.NewGuid() + ".db"), Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options).InitializeAsync(CancellationToken.None);
        return (options, await InsertGameAsync(options, "Test"));
    }

    private static async Task<GameId> InsertGameAsync(DatabaseOptions options, string title)
    {
        var gameId = GameId.New();
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={options.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES($id,$title,0,$utc,$utc);";
        command.Parameters.AddWithValue("$id", gameId.ToString());
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
        return gameId;
    }

    private static async Task InsertReferenceAsync(DatabaseOptions options, GameId gameId, ProviderKind provider, string externalId)
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={options.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO provider_game_refs(provider,external_id,game_id) VALUES($provider,$externalId,$gameId);";
        command.Parameters.AddWithValue("$provider", (int)provider);
        command.Parameters.AddWithValue("$externalId", externalId);
        command.Parameters.AddWithValue("$gameId", gameId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
