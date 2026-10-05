using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.ProviderActivity;
using PlayStead.Providers.Gog;

namespace PlayStead.Providers.Tests.Gog;

public sealed class GogLocalProviderActivitySourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-GogActivity-" + Guid.NewGuid().ToString("N"));
    private readonly string _db;

    public GogLocalProviderActivitySourceTests()
    {
        Directory.CreateDirectory(_root);
        _db = Path.Combine(_root, "galaxy-2.0.db");
    }

    [Fact]
    public async Task Reads_product_playtime_in_minutes_and_last_played()
    {
        CreateDatabase((1495134320, 23L, "2024-09-13 11:01:58"));
        var game = GameId.New();
        var value = Assert.Single(await ReadAsync(Installation(game, "1495134320")));

        Assert.Equal(game, value.GameId);
        Assert.Equal(ProviderKind.Gog, value.Provider);
        Assert.Equal("1495134320", value.ProviderGameId);
        Assert.Equal(TimeSpan.FromMinutes(23), value.TotalPlaytime);
        Assert.Null(value.LastPlayedAtUtc);
        Assert.Equal(ProviderActivityAvailability.Complete, value.Availability);
    }

    [Fact]
    public async Task Does_not_cross_contaminate_products()
    {
        CreateDatabase((1495134320, 23L, null), (1103900211, 7L, null));
        var first = Installation(GameId.New(), "1495134320");
        var second = Installation(GameId.New(), "1103900211");

        var values = await ReadAsync(first, second);

        Assert.Equal(TimeSpan.FromMinutes(23), values.Single(x => x.GameId == first.GameId).TotalPlaytime);
        Assert.Equal(TimeSpan.FromMinutes(7), values.Single(x => x.GameId == second.GameId).TotalPlaytime);
    }

    [Fact]
    public async Task Missing_database_returns_no_metadata()
    {
        var values = await ReadAsync(Installation(GameId.New(), "1495134320"));
        Assert.Empty(values);
    }

    [Fact]
    public async Task Missing_product_returns_unknown_metadata()
    {
        CreateDatabase();
        var value = Assert.Single(await ReadAsync(Installation(GameId.New(), "999")));
        Assert.Null(value.TotalPlaytime);
        Assert.Null(value.LastPlayedAtUtc);
        Assert.Equal(ProviderActivityAvailability.Unknown, value.Availability);
    }

    [Fact]
    public async Task Zero_minutes_is_known_zero_not_unknown()
    {
        CreateDatabase((1495134320, 0L, null));
        var value = Assert.Single(await ReadAsync(Installation(GameId.New(), "1495134320")));
        Assert.Equal(TimeSpan.Zero, value.TotalPlaytime);
        Assert.Equal(ProviderActivityAvailability.Complete, value.Availability);
    }

    [Fact]
    public async Task Invalid_negative_minutes_remains_unknown()
    {
        CreateDatabase((1495134320, -1L, null));
        var value = Assert.Single(await ReadAsync(Installation(GameId.New(), "1495134320")));
        Assert.Null(value.TotalPlaytime);
        Assert.Equal(ProviderActivityAvailability.Unknown, value.Availability);
    }

    private async Task<IReadOnlyList<ProviderActivityMetadata>> ReadAsync(params GameInstallation[] installations) =>
        await new GogLocalProviderActivitySource(_db).GetAsync(installations, CancellationToken.None);

    private GameInstallation Installation(GameId gameId, string productId) =>
        new(InstallationId.New(), gameId, ProviderKind.Gog, productId, _root, null, true, true, DateTimeOffset.UtcNow);

    private void CreateDatabase(params (long ProductId, long Minutes, string? LastPlayed)[] products)
    {
        var csb = new SqliteConnectionStringBuilder { DataSource = _db, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Private, Pooling = false };
        using var connection = new SqliteConnection(csb.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE ProductsToReleaseKeys (gogId INTEGER, releaseKey TEXT NOT NULL);
            CREATE TABLE GameTimes (userId INTEGER, releaseKey TEXT NOT NULL, minutesInGame INTEGER NOT NULL);
            CREATE TABLE LastPlayedDates (userId INTEGER, gameReleaseKey TEXT NOT NULL, lastPlayedDate TEXT NULL);
            """;
        command.ExecuteNonQuery();
        foreach (var product in products)
        {
            var releaseKey = "gog_" + product.ProductId;
            using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO ProductsToReleaseKeys(gogId,releaseKey) VALUES($id,$key); INSERT INTO GameTimes(userId,releaseKey,minutesInGame) VALUES(1,$key,$minutes);" +
                (product.LastPlayed is null ? string.Empty : " INSERT INTO LastPlayedDates(userId,gameReleaseKey,lastPlayedDate) VALUES(1,$key,$last);");
            insert.Parameters.AddWithValue("$id", product.ProductId);
            insert.Parameters.AddWithValue("$key", releaseKey);
            insert.Parameters.AddWithValue("$minutes", product.Minutes);
            insert.Parameters.AddWithValue("$last", (object?)product.LastPlayed ?? DBNull.Value);
            insert.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
