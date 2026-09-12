using PlayStead.Providers.Steam;
using PlayStead.Providers.Steam.Evidence;

namespace PlayStead.Providers.Tests.Steam.Evidence;

public sealed class SteamLocalEvidenceSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScanAsync_returns_empty_when_Steam_is_not_found()
    {
        var source = CreateSource(
            new WindowsSteamRootLocator(
                [Path.Combine(_root, "MissingSteam")]));

        var result = await source.ScanAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ScanAsync_collects_all_library_manifests_in_stable_app_id_order()
    {
        var steamRoot = Path.Combine(_root, "Steam");
        var secondaryRoot = Path.Combine(_root, "Secondary");

        var steamApps = Path.Combine(steamRoot, "steamapps");
        var secondaryApps = Path.Combine(secondaryRoot, "steamapps");

        Directory.CreateDirectory(steamApps);
        Directory.CreateDirectory(secondaryApps);

        File.WriteAllText(
            Path.Combine(steamApps, "libraryfolders.vdf"),
            $$"""
            "libraryfolders"
            {
                "0"
                {
                    "path" "{{EscapeValvePath(steamRoot)}}"
                }
                "1"
                {
                    "path" "{{EscapeValvePath(secondaryRoot)}}"
                }
            }
            """);

        WriteManifest(
            secondaryApps,
            appId: "440",
            buildId: "200",
            betaKey: "experimental",
            depotId: "441",
            manifestId: "333");

        WriteManifest(
            steamApps,
            appId: "730",
            buildId: "100",
            betaKey: null,
            depotId: "731",
            manifestId: "111");

        var source = CreateSource(
            new WindowsSteamRootLocator([steamRoot]));

        var result = await source.ScanAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(["440", "730"], result.Select(x => x.AppId).ToArray());

        var beta = Assert.Single(result, x => x.AppId == "440");
        Assert.Equal("experimental", beta.BranchName);
        Assert.Equal("333", beta.DepotManifestIds["441"]);

        var publicGame = Assert.Single(result, x => x.AppId == "730");
        Assert.Equal("public", publicGame.BranchName);
        Assert.Equal("111", publicGame.DepotManifestIds["731"]);
    }

    [Fact]
    public async Task ScanAsync_honors_pre_cancelled_token()
    {
        var steamRoot = Path.Combine(_root, "Steam");
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var source = CreateSource(
            new WindowsSteamRootLocator([steamRoot]));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => source.ScanAsync(cts.Token));
    }

    private static SteamLocalEvidenceSource CreateSource(
        WindowsSteamRootLocator rootLocator)
        => new(
            rootLocator,
            new SteamLibraryFoldersReader(),
            new SteamLocalEvidenceReader());

    private static void WriteManifest(
        string steamApps,
        string appId,
        string buildId,
        string? betaKey,
        string depotId,
        string manifestId)
    {
        var betaBlock = betaKey is null
            ? string.Empty
            : $$"""
                "UserConfig"
                {
                    "BetaKey" "{{betaKey}}"
                }
            """;

        File.WriteAllText(
            Path.Combine(steamApps, $"appmanifest_{appId}.acf"),
            $$"""
            "AppState"
            {
                "appid" "{{appId}}"
                "buildid" "{{buildId}}"
                {{betaBlock}}
                "InstalledDepots"
                {
                    "{{depotId}}"
                    {
                        "manifest" "{{manifestId}}"
                    }
                }
            }
            """);
    }

    private static string EscapeValvePath(string path) =>
        path.Replace(@"\", @"\\", StringComparison.Ordinal);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
