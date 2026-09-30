using System.Text.Json;
using PlayStead.Core.Library;
using PlayStead.Providers.Gog;

namespace PlayStead.Providers.Tests.Gog;

public sealed class GogLocalLibrarySourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-Gog-" + Guid.NewGuid().ToString("N"));

    public GogLocalLibrarySourceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Parses_goggame_info_as_installed_projection_with_stable_id()
    {
        var install = CreateInstall("Crysis Remastered");
        WriteInfo(install, "1103900211", "Crysis Remastered");

        var result = await ScanAsync(install);
        var item = Assert.Single(result.Installations);

        Assert.Equal(ProviderKind.Gog, item.Provider);
        Assert.Equal("1103900211", item.ExternalId);
        Assert.Equal("Crysis Remastered", item.Title);
        Assert.Equal(Path.GetFullPath(install), item.InstallPath);
        Assert.Null(item.InstalledSizeBytes);
        Assert.Equal(InstallationContentKind.Game, item.ContentKind);
    }

    [Fact]
    public async Task Invalid_entry_is_isolated_from_valid_standalone_install()
    {
        var valid = CreateInstall("Valid");
        WriteInfo(valid, "100", "Valid Game");
        var broken = CreateInstall("broken");
        File.WriteAllText(Path.Combine(broken, "goggame-200.info"), "{ invalid");

        var result = await ScanAsync(valid, Path.Combine(_root, "broken"));

        Assert.Single(result.Installations);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task Missing_or_stale_entry_is_not_published()
    {
        var missing = Path.Combine(_root, "Missing");
        Directory.CreateDirectory(Path.Combine(_root, "stale"));
        WriteInfo(Path.Combine(_root, "stale"), "300", "Stale");
        Directory.Delete(Path.Combine(_root, "stale"), recursive: true);

        var result = await ScanAsync(missing, Path.Combine(_root, "stale"));

        Assert.Empty(result.Installations);
    }

    [Fact]
    public async Task Same_title_with_different_ids_stays_separate_and_duplicates_are_deduplicated()
    {
        var first = CreateInstall("first");
        var second = CreateInstall("second");
        WriteInfo(first, "400", "Same Title");
        WriteInfo(second, "401", "Same Title");

        var result = await ScanAsync(first, second);

        Assert.Equal(2, result.Installations.Count);
        Assert.Equal(["400", "401"], result.Installations.Select(x => x.ExternalId).OrderBy(x => x).ToArray());
        Assert.DoesNotContain(result.Installations, x => x.Title == x.ExternalId);
    }

    [Fact]
    public async Task Empty_title_is_not_used_as_identity_or_published()
    {
        var install = CreateInstall("NoTitle");
        WriteInfo(install, "500", null);

        var result = await ScanAsync(install);

        Assert.Empty(result.Installations);
        Assert.Contains(result.Warnings, warning => warning.Contains("missing-display-name", StringComparison.Ordinal));
    }

    private async Task<PlayStead.Core.Scanning.SourceScanResult> ScanAsync(params string[] roots) =>
        await new GogLocalLibrarySource(roots).ScanAsync(CancellationToken.None);

    private string CreateInstall(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteInfo(string directory, string id, string? name, string suffix = "")
    {
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, $"goggame-{id}{suffix}.info");
        File.WriteAllText(file, JsonSerializer.Serialize(new { gameId = id, name }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
