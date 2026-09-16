using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.Data.Library;

namespace PlayStead.Data.Tests.Library;

public sealed class SqliteLibraryGameLookupTests : IDisposable
{
    private static readonly DateTimeOffset ObservedAt =
        DateTimeOffset.Parse("2026-09-16T18:00:00Z");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        "LibraryGameLookup",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Existing_provider_ref_returns_exact_game_id()
    {
        var (options, gameId) = await CreateDatabaseWithProviderRefAsync();
        var sut = new SqliteLibraryGameLookup(options);

        var result = await sut.FindGameIdByProviderRefAsync(
            ProviderKind.Steam,
            "1874880",
            CancellationToken.None);

        Assert.Equal(gameId, result);
    }

    [Fact]
    public async Task Unknown_provider_ref_returns_null()
    {
        var (options, _) = await CreateDatabaseWithProviderRefAsync();
        var sut = new SqliteLibraryGameLookup(options);

        var result = await sut.FindGameIdByProviderRefAsync(
            ProviderKind.Steam,
            "9999999",
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Steam_provider_ref_persisted_by_scan_returns_exact_game_id()
    {
        var options = await CreateDatabaseAsync("scan.db");
        var store = new SqliteLibraryStore(options);
        await store.ApplySourceScanAsync(
            SourceScanResult.Success(
                ProviderKind.Steam,
                ObservedAt,
                [
                    DiscoveredInstallation.Create(
                        ProviderKind.Steam,
                        "730",
                        "Counter-Strike 2",
                        @"G:\Games\CS2",
                        42,
                        ObservedAt)
                ]),
            CancellationToken.None);
        var expected = Assert.Single(
            (await store.LoadSnapshotAsync(CancellationToken.None)).Games).Id;
        var sut = new SqliteLibraryGameLookup(options);

        var result = await sut.FindGameIdByProviderRefAsync(
            ProviderKind.Steam,
            "730",
            CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Already_cancelled_token_is_propagated()
    {
        var options = await CreateDatabaseAsync("cancelled.db");
        var sut = new SqliteLibraryGameLookup(options);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.FindGameIdByProviderRefAsync(
                ProviderKind.Steam,
                "1874880",
                cancellation.Token));
    }

    private async Task<(DatabaseOptions Options, GameId GameId)>
        CreateDatabaseWithProviderRefAsync()
    {
        var options = await CreateDatabaseAsync("provider-ref.db");
        var gameId = new GameId(
            Guid.Parse("11111111-1111-4111-8111-111111111111"));

        await using var connection = new SqliteConnection(
            $"Data Source={options.DatabasePath};Pooling=False");
        await connection.OpenAsync();

        var insertGame = connection.CreateCommand();
        insertGame.CommandText = "INSERT INTO games(game_id,title,is_hidden,created_utc,updated_utc) VALUES ($gameId,$title,0,$utc,$utc);";
        insertGame.Parameters.AddWithValue("$gameId", gameId.ToString());
        insertGame.Parameters.AddWithValue("$title", "Arma Reforger");
        insertGame.Parameters.AddWithValue("$utc", ObservedAt.ToString("O"));
        await insertGame.ExecuteNonQueryAsync();

        var insertReference = connection.CreateCommand();
        insertReference.CommandText = "INSERT INTO provider_game_refs(provider,external_id,game_id) VALUES ($provider,$externalId,$gameId);";
        insertReference.Parameters.AddWithValue("$provider", (int)ProviderKind.Steam);
        insertReference.Parameters.AddWithValue("$externalId", "1874880");
        insertReference.Parameters.AddWithValue("$gameId", gameId.ToString());
        await insertReference.ExecuteNonQueryAsync();

        return (options, gameId);
    }

    private async Task<DatabaseOptions> CreateDatabaseAsync(string fileName)
    {
        Directory.CreateDirectory(_root);
        var options = new DatabaseOptions(
            Path.Combine(_root, fileName),
            Path.Combine(_root, "Backups"));
        await new DatabaseInitializer(options)
            .InitializeAsync(CancellationToken.None);
        return options;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
