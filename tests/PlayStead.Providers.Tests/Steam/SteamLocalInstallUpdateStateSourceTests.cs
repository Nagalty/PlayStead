using PlayStead.Core.Library;
using PlayStead.Core.ProviderInstallUpdate;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamLocalInstallUpdateStateSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetAsync_reads_pending_update_from_local_manifest()
    {
        var steamRoot = Path.Combine(_root, "Steam");
        var steamApps = Path.Combine(steamRoot, "steamapps");
        Directory.CreateDirectory(steamApps);
        File.WriteAllText(Path.Combine(steamApps, "libraryfolders.vdf"), $$"""
            "libraryfolders"
            {
                "0" { "path" "{{EscapeValvePath(steamRoot)}}" }
            }
            """);
        File.Copy(
            FixturePath(Path.Combine("InstallUpdate", "appmanifest_553850_pending.acf")),
            Path.Combine(steamApps, "appmanifest_553850.acf"));

        var gameId = new GameId(Guid.NewGuid());
        var installation = new GameInstallation(
            InstallationId.New(), gameId, ProviderKind.Steam, "553850",
            Path.Combine(steamApps, "common", "Helldivers 2"), null, true, true, DateTimeOffset.UtcNow);
        var source = new SteamLocalInstallUpdateStateSource(
            new WindowsSteamRootLocator([steamRoot]),
            new SteamLibraryFoldersReader(),
            new SteamAppManifestReader(),
            new SteamAppInfoReader(_ => new MemoryStream(Array.Empty<byte>())),
            new ProviderInstallUpdateStateEvaluator());

        var values = await source.GetAsync([installation], CancellationToken.None);
        var state = Assert.Single(values);

        Assert.Equal(gameId, state.GameId);
        Assert.Equal(ProviderInstallUpdateStatus.UpdateAvailable, state.Status);
        Assert.Equal(86653644, state.BytesToDownload);
    }

    [Fact]
    public async Task GetAsync_returns_unknown_when_manifest_is_missing()
    {
        var steamRoot = Path.Combine(_root, "MissingSteam");
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        var installation = new GameInstallation(
            InstallationId.New(), new GameId(Guid.NewGuid()), ProviderKind.Steam, "553850",
            Path.Combine(steamRoot, "steamapps", "common", "Helldivers 2"), null, true, true, DateTimeOffset.UtcNow);
        var source = new SteamLocalInstallUpdateStateSource(
            new WindowsSteamRootLocator([steamRoot]),
            new SteamLibraryFoldersReader(),
            new SteamAppManifestReader(),
            new SteamAppInfoReader(_ => new MemoryStream(Array.Empty<byte>())),
            new ProviderInstallUpdateStateEvaluator());

        var state = Assert.Single(await source.GetAsync([installation], CancellationToken.None));

        Assert.Equal(ProviderInstallUpdateStatus.Unknown, state.Status);
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Steam", name);

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
