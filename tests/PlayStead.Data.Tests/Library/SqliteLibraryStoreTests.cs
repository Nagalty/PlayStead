using Microsoft.Data.Sqlite;
using PlayStead.Core.Library;
using PlayStead.Core.Persistence;
using PlayStead.Core.Scanning;
using PlayStead.Data.Database;
using PlayStead.Data.Library;

namespace PlayStead.Data.Tests.Library;

public sealed class SqliteLibraryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task First_complete_scan_creates_game_provider_ref_and_installation()
    {
        var (store, databasePath) = await CreateStoreAsync();

        var observed = Utc(8, 0);
        var scan = SourceScanResult.Success(
            ProviderKind.Steam,
            observed,
            [SteamGame(observed)]);

        await store.ApplySourceScanAsync(scan, CancellationToken.None);

        var snapshot = await store.LoadSnapshotAsync(CancellationToken.None);

        var game = Assert.Single(snapshot.Games);
        var installation = Assert.Single(snapshot.Installations);

        Assert.Equal("Counter-Strike 2", game.Title);
        Assert.Equal(game.Id, installation.GameId);
        Assert.Equal(ProviderKind.Steam, installation.Provider);
        Assert.Equal("730", installation.ExternalId);
        Assert.True(installation.IsPresent);
        Assert.Equal(observed, installation.LastSeenUtc);

        Assert.Equal(
            1,
            await CountProviderRefsAsync(
                databasePath,
                ProviderKind.Steam,
                "730"));
    }

    [Fact]
    public async Task Second_identical_scan_reuses_game_and_installation_identity()
    {
        var (store, _) = await CreateStoreAsync();

        var firstObserved = Utc(8, 0);
        await store.ApplySourceScanAsync(
            SourceScanResult.Success(
                ProviderKind.Steam,
                firstObserved,
                [SteamGame(firstObserved)]),
            CancellationToken.None);

        var first = await store.LoadSnapshotAsync(CancellationToken.None);
        var firstGameId = Assert.Single(first.Games).Id;
        var firstInstallationId = Assert.Single(first.Installations).Id;

        var secondObserved = Utc(8, 30);
        await store.ApplySourceScanAsync(
            SourceScanResult.Success(
                ProviderKind.Steam,
                secondObserved,
                [SteamGame(secondObserved)]),
            CancellationToken.None);

        var second = await store.LoadSnapshotAsync(CancellationToken.None);

        Assert.Equal(firstGameId, Assert.Single(second.Games).Id);

        var installation = Assert.Single(second.Installations);
        Assert.Equal(firstInstallationId, installation.Id);
        Assert.True(installation.IsPresent);
        Assert.Equal(secondObserved, installation.LastSeenUtc);
    }

    [Fact]
    public async Task Later_complete_scan_missing_game_marks_installation_absent_without_deleting_it()
    {
        var (store, _) = await CreateStoreAsync();

        var firstObserved = Utc(8, 0);
        await store.ApplySourceScanAsync(
            SourceScanResult.Success(
                ProviderKind.Steam,
                firstObserved,
                [SteamGame(firstObserved)]),
            CancellationToken.None);

        await store.ApplySourceScanAsync(
            SourceScanResult.Success(
                ProviderKind.Steam,
                Utc(9, 0),
                Array.Empty<DiscoveredInstallation>()),
            CancellationToken.None);

        var snapshot = await store.LoadSnapshotAsync(CancellationToken.None);

        Assert.Single(snapshot.Games);

        var installation = Assert.Single(snapshot.Installations);
        Assert.False(installation.IsPresent);
        Assert.Equal(firstObserved, installation.LastSeenUtc);
    }

    [Fact]
    public async Task Incomplete_scan_does_not_mark_existing_installation_absent()
    {
        var (store, _) = await CreateStoreAsync();

        var firstObserved = Utc(8, 0);
        await store.ApplySourceScanAsync(
            SourceScanResult.Success(
                ProviderKind.Steam,
                firstObserved,
                [SteamGame(firstObserved)]),
            CancellationToken.None);

        await store.ApplySourceScanAsync(
            SourceScanResult.Failure(
                ProviderKind.Steam,
                Utc(9, 0),
                "IOException",
                "Steam library temporarily unavailable."),
            CancellationToken.None);

        var snapshot = await store.LoadSnapshotAsync(CancellationToken.None);

        var installation = Assert.Single(snapshot.Installations);
        Assert.True(installation.IsPresent);
        Assert.Equal(firstObserved, installation.LastSeenUtc);
    }

    private async Task<(ILibraryStore Store, string DatabasePath)> CreateStoreAsync()
    {
        Directory.CreateDirectory(_root);

        var databasePath = Path.Combine(_root, "playstead.db");
        var options = new DatabaseOptions(
            databasePath,
            Path.Combine(_root, "Backups"));

        await new DatabaseInitializer(options)
            .InitializeAsync(CancellationToken.None);

        ILibraryStore store = new SqliteLibraryStore(options);

        return (store, databasePath);
    }

    private static DiscoveredInstallation SteamGame(
        DateTimeOffset observedAtUtc) =>
        DiscoveredInstallation.Create(
            ProviderKind.Steam,
            "730",
            "Counter-Strike 2",
            @"G:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive",
            42_000_000_000,
            observedAtUtc);

    private static DateTimeOffset Utc(int hour, int minute) =>
        new(2026, 9, 12, hour, minute, 0, TimeSpan.Zero);

    private static async Task<int> CountProviderRefsAsync(
        string databasePath,
        ProviderKind provider,
        string externalId)
    {
        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Mode=ReadOnly;Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM provider_game_refs
            WHERE provider = $provider
              AND external_id = $externalId;
            """;
        command.Parameters.AddWithValue("$provider", (int)provider);
        command.Parameters.AddWithValue("$externalId", externalId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
