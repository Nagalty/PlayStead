using Microsoft.Data.Sqlite;
using PlayStead.Core.Catalog;
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
        Assert.Null(game.CanonicalContentId);
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
    public async Task Epic_launch_metadata_survives_scan_and_reload()
    {
        var (store, _) = await CreateStoreAsync();
        var observed = Utc(8, 0);
        var discovered = DiscoveredInstallation.Create(
            ProviderKind.Epic,
            "581c8d4fd9574884bff66cbdbaa42def",
            "Hell Let Loose",
            @"G:\HellLetLooseG0WU4",
            65_671_088_232,
            observed) with
        {
            LaunchMetadata = new ProviderLaunchMetadata(
                ProviderKind.Epic,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["CatalogNamespace"] = "6430e58041234e41b8f81f68f01450ed",
                    ["CatalogItemId"] = "581c8d4fd9574884bff66cbdbaa42def",
                    ["AppName"] = "3e02273b543f4ff0a1c24d3b534a9ac3"
                })
        };

        await store.ApplySourceScanAsync(
            SourceScanResult.Success(ProviderKind.Epic, observed, [discovered with { LaunchMetadata = null }]),
            CancellationToken.None);

        await store.ApplySourceScanAsync(
            SourceScanResult.Success(ProviderKind.Epic, observed.AddMinutes(5), [discovered]),
            CancellationToken.None);

        var installation = Assert.Single((await store.LoadSnapshotAsync(CancellationToken.None)).Installations);
        Assert.Equal("6430e58041234e41b8f81f68f01450ed", installation.LaunchMetadata?["CatalogNamespace"]);
        Assert.Equal("3e02273b543f4ff0a1c24d3b534a9ac3", installation.LaunchMetadata?["AppName"]);
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

        Assert.Null(
            Assert.Single(second.Games)
                .CanonicalContentId);

        var installation = Assert.Single(second.Installations);
        Assert.Equal(firstInstallationId, installation.Id);
        Assert.True(installation.IsPresent);
        Assert.Equal(secondObserved, installation.LastSeenUtc);
    }

    [Fact]
    public async Task LoadSnapshot_reads_canonical_content_id_when_present()
    {
        var (store, databasePath) = await CreateStoreAsync();

        var observed = Utc(8, 0);
        await store.ApplySourceScanAsync(
            SourceScanResult.Success(
                ProviderKind.Steam,
                observed,
                [SteamGame(observed)]),
            CancellationToken.None);

        var canonicalId =
            new CatalogContentId(
                Guid.Parse(
                    "22222222-2222-2222-2222-222222222222"));

        await using (var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False"))
        {
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE games
                SET canonical_content_id = $canonicalContentId;
                """;

            command.Parameters.AddWithValue(
                "$canonicalContentId",
                canonicalId.Value.ToString("D"));

            await command.ExecuteNonQueryAsync();
        }

        var snapshot =
            await store.LoadSnapshotAsync(
                CancellationToken.None);

        var game = Assert.Single(snapshot.Games);

        Assert.Equal(
            canonicalId,
            game.CanonicalContentId);
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

    [Fact]
    public async Task Manual_game_persists_stable_id_launch_settings_and_non_destructive_removal()
    {
        var (store, _) = await CreateStoreAsync();
        var manual = Assert.IsAssignableFrom<IManualGameStore>(store);
        var root = Path.Combine(_root, "ManualGame");
        Directory.CreateDirectory(root);
        var installRoot = Path.Combine(_root, "ManualInstallRoot");
        Directory.CreateDirectory(installRoot);
        var executable = Path.Combine(root, "game.exe");
        File.WriteAllText(executable, string.Empty);
        var definition = ManualGameDefinition.Create("My Manual Game", executable, root, "--safe", installRoot);

        var created = await manual.CreateAsync(definition, CancellationToken.None);
        var reloaded = await store.LoadSnapshotAsync(CancellationToken.None);
        var persisted = Assert.Single(reloaded.Installations);
        Assert.Equal(created.GameId, persisted.GameId);
        Assert.Equal(created.ExternalId, persisted.ExternalId);
        Assert.Equal(ProviderKind.Manual, persisted.Provider);
        Assert.Equal(executable, persisted.ExecutablePath);
        Assert.Equal(root, persisted.WorkingDirectory);
        Assert.Equal(installRoot, persisted.InstallPath);
        Assert.Equal(installRoot, persisted.InstallRootPath);
        Assert.Equal("--safe", persisted.LaunchArguments);

        var renamed = ManualGameDefinition.Create("Renamed Game", executable, root, "--other");
        var updated = await manual.UpdateAsync(created.GameId, renamed, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal(created.ExternalId, updated!.ExternalId);
        Assert.Equal("Renamed Game", (await store.LoadSnapshotAsync(CancellationToken.None)).Games.Single().Title);

        var sameTitle = ManualGameDefinition.Create("Renamed Game", executable, root, "--second");
        var second = await manual.CreateAsync(sameTitle, CancellationToken.None);
        var distinctGames = (await store.LoadSnapshotAsync(CancellationToken.None)).Games;
        Assert.Equal(2, distinctGames.Count);
        Assert.NotEqual(created.GameId, second.GameId);
        Assert.NotEqual(created.ExternalId, second.ExternalId);

        Assert.True(await manual.RemoveAsync(created.GameId, CancellationToken.None));
        Assert.True(File.Exists(executable));
        var afterRemoval = (await store.LoadSnapshotAsync(CancellationToken.None)).Installations;
        Assert.False(afterRemoval.Single(i => i.GameId == created.GameId).IsPresent);
        Assert.True(afterRemoval.Single(i => i.GameId == second.GameId).IsPresent);
    }

    [Fact]
    public async Task Legacy_manual_row_without_install_root_falls_back_to_working_directory()
    {
        var (store, databasePath) = await CreateStoreAsync();
        var manual = Assert.IsAssignableFrom<IManualGameStore>(store);
        var root = Directory.CreateDirectory(Path.Combine(_root, "LegacyManual")).FullName;
        var executable = Path.Combine(root, "game.exe");
        File.WriteAllText(executable, string.Empty);
        var created = await manual.CreateAsync(
            ManualGameDefinition.Create("Legacy", executable, root),
            CancellationToken.None);

        await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE installations SET install_root_path=NULL WHERE game_id=$gameId;";
            command.Parameters.AddWithValue("$gameId", created.GameId.ToString());
            await command.ExecuteNonQueryAsync();
        }

        var reloaded = (await store.LoadSnapshotAsync(CancellationToken.None)).Installations
            .Single(i => i.GameId == created.GameId);
        Assert.Equal(root, reloaded.WorkingDirectory);
        Assert.Equal(root, reloaded.InstallPath);
        Assert.Equal(root, reloaded.InstallRootPath);
    }

    [Fact]
    public async Task Legacy_retail_root_equal_to_working_directory_is_repaired_on_projection()
    {
        var (store, databasePath) = await CreateStoreAsync();
        var manual = Assert.IsAssignableFrom<IManualGameStore>(store);
        var gameRoot = Directory.CreateDirectory(Path.Combine(_root, "RetailGame")).FullName;
        var workingDirectory = Directory.CreateDirectory(Path.Combine(gameRoot, "Retail")).FullName;
        var executable = Path.Combine(workingDirectory, "007FirstLight.exe");
        File.WriteAllText(executable, string.Empty);
        var created = await manual.CreateAsync(
            ManualGameDefinition.Create("007 First Light", executable, workingDirectory, null, workingDirectory),
            CancellationToken.None);

        var reloaded = (await store.LoadSnapshotAsync(CancellationToken.None)).Installations
            .Single(i => i.GameId == created.GameId);

        Assert.Equal(workingDirectory, reloaded.WorkingDirectory);
        Assert.Equal(gameRoot, reloaded.InstallRootPath);
        Assert.Equal(gameRoot, reloaded.InstallPath);

        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT install_root_path FROM installations WHERE game_id=$gameId;";
        command.Parameters.AddWithValue("$gameId", created.GameId.ToString());
        Assert.Equal(workingDirectory, (string?)await command.ExecuteScalarAsync());
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
