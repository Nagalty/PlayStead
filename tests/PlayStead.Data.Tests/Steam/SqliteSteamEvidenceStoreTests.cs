using Microsoft.Data.Sqlite;
using PlayStead.Core.Steam;
using PlayStead.Data.Database;
using PlayStead.Data.Steam;

namespace PlayStead.Data.Tests.Steam;

public sealed class SqliteSteamEvidenceStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ReplaceLocal_round_trips_branch_build_depots_and_timestamp()
    {
        var context = await CreateStoreAsync();

        var expected = new SteamLocalEvidence(
            "730",
            "100",
            "public",
            new Dictionary<string, string>
            {
                ["731"] = "111",
                ["732"] = "222"
            },
            new DateTimeOffset(
                2026, 9, 12, 18, 0, 0, TimeSpan.Zero));

        await context.Store.ReplaceLocalAsync(
            [expected],
            CancellationToken.None);

        var actual = await context.Store.GetLocalAsync(
            CancellationToken.None);

        var item = Assert.Single(actual);

        Assert.Equal(expected.AppId, item.AppId);
        Assert.Equal(expected.BuildId, item.BuildId);
        Assert.Equal(expected.BranchName, item.BranchName);
        Assert.Equal(expected.ObservedAtUtc, item.ObservedAtUtc);
        Assert.Equal(
            expected.DepotManifestIds.OrderBy(x => x.Key),
            item.DepotManifestIds.OrderBy(x => x.Key));
    }

    [Fact]
    public async Task ReplaceLocal_replaces_the_entire_previous_snapshot()
    {
        var context = await CreateStoreAsync();

        await context.Store.ReplaceLocalAsync(
            [
                Local("440", "beta", "200", ("441", "333")),
                Local("730", "public", "100", ("731", "111"))
            ],
            CancellationToken.None);

        await context.Store.ReplaceLocalAsync(
            [
                Local("570", "public", "300", ("571", "444"))
            ],
            CancellationToken.None);

        var actual = await context.Store.GetLocalAsync(
            CancellationToken.None);

        var item = Assert.Single(actual);
        Assert.Equal("570", item.AppId);
        Assert.Equal("444", item.DepotManifestIds["571"]);
    }

    [Fact]
    public async Task ReplaceLocal_with_empty_snapshot_clears_previous_local_evidence()
    {
        var context = await CreateStoreAsync();

        await context.Store.ReplaceLocalAsync(
            [Local("730", "public", "100", ("731", "111"))],
            CancellationToken.None);

        await context.Store.ReplaceLocalAsync(
            Array.Empty<SteamLocalEvidence>(),
            CancellationToken.None);

        Assert.Empty(
            await context.Store.GetLocalAsync(
                CancellationToken.None));
    }

    [Fact]
    public async Task UpsertRemote_replaces_only_the_same_app_and_branch()
    {
        var context = await CreateStoreAsync();

        await context.Store.UpsertRemoteAsync(
            Remote("730", "public", "100", ("731", "111")),
            CancellationToken.None);

        await context.Store.UpsertRemoteAsync(
            Remote("730", "experimental", "200", ("731", "222")),
            CancellationToken.None);

        await context.Store.UpsertRemoteAsync(
            Remote("730", "public", "101", ("731", "999")),
            CancellationToken.None);

        var publicEvidence = await context.Store.GetRemoteAsync(
            "730",
            "public",
            CancellationToken.None);

        var betaEvidence = await context.Store.GetRemoteAsync(
            "730",
            "experimental",
            CancellationToken.None);

        Assert.NotNull(publicEvidence);
        Assert.NotNull(betaEvidence);

        Assert.Equal("101", publicEvidence!.BuildId);
        Assert.Equal("999", publicEvidence.DepotManifestIds["731"]);

        Assert.Equal("200", betaEvidence!.BuildId);
        Assert.Equal("222", betaEvidence.DepotManifestIds["731"]);
    }

    [Fact]
    public async Task ClearRemote_removes_remote_evidence_but_preserves_local_evidence()
    {
        var context = await CreateStoreAsync();

        await context.Store.ReplaceLocalAsync(
            [Local("730", "public", "100", ("731", "111"))],
            CancellationToken.None);

        await context.Store.UpsertRemoteAsync(
            Remote("730", "public", "101", ("731", "999")),
            CancellationToken.None);

        await context.Store.ClearRemoteAsync(
            CancellationToken.None);

        Assert.Empty(
            await context.Store.GetAllRemoteAsync(
                CancellationToken.None));

        Assert.Single(
            await context.Store.GetLocalAsync(
                CancellationToken.None));
    }

    [Fact]
    public async Task Depot_manifest_json_is_stored_in_stable_depot_id_order()
    {
        var context = await CreateStoreAsync();

        await context.Store.UpsertRemoteAsync(
            new SteamRemoteEvidence(
                "730",
                "public",
                "101",
                new Dictionary<string, string>
                {
                    ["300"] = "ccc",
                    ["100"] = "aaa",
                    ["200"] = "bbb"
                },
                new DateTimeOffset(
                    2026, 9, 12, 18, 30, 0, TimeSpan.Zero),
                SteamRemoteEvidenceSource.SteamCmdAnonymous),
            CancellationToken.None);

        await using var connection = new SqliteConnection(
            $"Data Source={context.DatabasePath};Pooling=False");

        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = """
            SELECT depot_manifests_json
            FROM steam_remote_evidence
            WHERE app_id = '730'
              AND branch_name = 'public';
            """;

        var json = Convert.ToString(
            await command.ExecuteScalarAsync());

        Assert.Equal(
            """{"100":"aaa","200":"bbb","300":"ccc"}""",
            json);
    }

    [Fact]
    public async Task GetAllRemote_returns_stable_app_and_branch_order()
    {
        var context = await CreateStoreAsync();

        await context.Store.UpsertRemoteAsync(
            Remote("730", "public", "100", ("731", "111")),
            CancellationToken.None);

        await context.Store.UpsertRemoteAsync(
            Remote("440", "experimental", "200", ("441", "222")),
            CancellationToken.None);

        await context.Store.UpsertRemoteAsync(
            Remote("440", "public", "201", ("441", "333")),
            CancellationToken.None);

        var actual = await context.Store.GetAllRemoteAsync(
            CancellationToken.None);

        Assert.Equal(
            [
                "440:experimental",
                "440:public",
                "730:public"
            ],
            actual.Select(
                x => $"{x.AppId}:{x.BranchName}").ToArray());
    }

    private async Task<StoreContext> CreateStoreAsync()
    {
        var databasePath = Path.Combine(
            _root,
            $"{Guid.NewGuid():N}.db");

        var backupsDirectory = Path.Combine(
            _root,
            "Backups");

        Directory.CreateDirectory(_root);

        var options = new DatabaseOptions(
            databasePath,
            backupsDirectory);

        await new DatabaseInitializer(options)
            .InitializeAsync(CancellationToken.None);

        return new StoreContext(
            new SqliteSteamEvidenceStore(options),
            databasePath);
    }

    private static SteamLocalEvidence Local(
        string appId,
        string branch,
        string build,
        params (string DepotId, string ManifestId)[] depots)
        => new(
            appId,
            build,
            branch,
            DepotMap(depots),
            new DateTimeOffset(
                2026, 9, 12, 18, 0, 0, TimeSpan.Zero));

    private static SteamRemoteEvidence Remote(
        string appId,
        string branch,
        string build,
        params (string DepotId, string ManifestId)[] depots)
        => new(
            appId,
            branch,
            build,
            DepotMap(depots),
            new DateTimeOffset(
                2026, 9, 12, 18, 30, 0, TimeSpan.Zero),
            SteamRemoteEvidenceSource.SteamCmdAnonymous);

    private static IReadOnlyDictionary<string, string> DepotMap(
        params (string DepotId, string ManifestId)[] depots)
        => depots.ToDictionary(
            x => x.DepotId,
            x => x.ManifestId,
            StringComparer.Ordinal);

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

    private sealed record StoreContext(
        SqliteSteamEvidenceStore Store,
        string DatabasePath);
}
