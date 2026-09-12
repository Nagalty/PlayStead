using PlayStead.Core.Library;
using PlayStead.Providers.Steam;

namespace PlayStead.Providers.Tests.Steam;

public sealed class SteamLocalLibrarySourceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "PlayStead.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScanAsync_returns_complete_empty_result_when_Steam_is_not_found()
    {
        var source = new SteamLocalLibrarySource(
            new WindowsSteamRootLocator(
                [Path.Combine(_root, "MissingSteam")]),
            new SteamLibraryFoldersReader(),
            new SteamAppManifestReader());

        var result = await source.ScanAsync(CancellationToken.None);

        Assert.Equal(ProviderKind.Steam, result.Provider);
        Assert.True(result.IsComplete);
        Assert.Empty(result.Installations);
        Assert.Empty(result.Warnings);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ScanAsync_discovers_only_present_installations_and_keeps_manifest_failures_as_warnings()
    {
        var steamRoot = Path.Combine(_root, "Steam");
        var steamApps = Path.Combine(steamRoot, "steamapps");
        var common = Path.Combine(steamApps, "common");

        Directory.CreateDirectory(common);

        File.WriteAllText(
            Path.Combine(steamApps, "libraryfolders.vdf"),
            $$"""
            "libraryfolders"
            {
                "0"
                {
                    "path" "{{EscapeValvePath(steamRoot)}}"
                }
            }
            """);

        WriteManifest(
            steamApps,
            appId: "730",
            name: "Counter-Strike 2",
            installDir: "Counter-Strike Global Offensive",
            sizeOnDisk: 42_000_000_000);

        Directory.CreateDirectory(
            Path.Combine(common, "Counter-Strike Global Offensive"));

        WriteManifest(
            steamApps,
            appId: "999",
            name: "Not Actually Installed",
            installDir: "Missing Game",
            sizeOnDisk: 123);

        File.WriteAllText(
            Path.Combine(steamApps, "appmanifest_123.acf"),
            "\"AppState\" { \"appid\"");

        var source = new SteamLocalLibrarySource(
            new WindowsSteamRootLocator([steamRoot]),
            new SteamLibraryFoldersReader(),
            new SteamAppManifestReader());

        var result = await source.ScanAsync(CancellationToken.None);

        Assert.True(result.IsComplete);

        var installation = Assert.Single(result.Installations);
        Assert.Equal(ProviderKind.Steam, installation.Provider);
        Assert.Equal("730", installation.ExternalId);
        Assert.Equal("Counter-Strike 2", installation.Title);
        Assert.Equal(
            Path.GetFullPath(
                Path.Combine(
                    common,
                    "Counter-Strike Global Offensive")),
            installation.InstallPath);

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("appmanifest_123.acf", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(FormatException), warning, StringComparison.Ordinal);
    }

    private static void WriteManifest(
        string steamApps,
        string appId,
        string name,
        string installDir,
        long sizeOnDisk)
    {
        File.WriteAllText(
            Path.Combine(steamApps, $"appmanifest_{appId}.acf"),
            $$"""
            "AppState"
            {
                "appid" "{{appId}}"
                "name" "{{name}}"
                "installdir" "{{installDir}}"
                "SizeOnDisk" "{{sizeOnDisk}}"
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
