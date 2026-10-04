using System.Text.Json;
using PlayStead.Core.Library;
using PlayStead.Core.Scanning;
using PlayStead.Providers.Epic;

namespace PlayStead.Providers.Tests.Epic;

public sealed class EpicLocalLibrarySourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PlayStead-Epic-" + Guid.NewGuid().ToString("N"));
    private readonly string _installPath;

    public EpicLocalLibrarySourceTests()
    {
        _installPath = Path.Combine(_root, "Install");
        Directory.CreateDirectory(_installPath);
        Directory.CreateDirectory(Path.Combine(_root, "Manifests"));
    }

    [Fact]
    public async Task Parses_real_manifest_fields_into_provider_projection()
    {
        WriteManifest("valid.item", new
        {
            DisplayName = "Hell Let Loose",
            InstallLocation = _installPath,
            InstallSize = 65671088232L,
            LaunchExecutable = "Launch_HLL.exe",
            CatalogItemId = "catalog-id",
            AppName = "app-name",
            CatalogNamespace = "namespace",
            bIsIncompleteInstall = false,
            InstallationGuid = "installation-guid"
        });

        var result = await ScanAsync();
        var installation = Assert.Single(result.Installations);

        Assert.Equal(ProviderKind.Epic, installation.Provider);
        Assert.Equal("catalog-id", installation.ExternalId);
        Assert.Equal("Hell Let Loose", installation.Title);
        Assert.Equal(Path.GetFullPath(_installPath), installation.InstallPath);
        Assert.Equal(65671088232L, installation.InstalledSizeBytes);
        Assert.Equal(InstallationContentKind.Game, installation.ContentKind);
        Assert.Equal(ProviderKind.Epic, installation.LaunchMetadata?.Provider);
        Assert.Equal("namespace", installation.LaunchMetadata?["CatalogNamespace"]);
        Assert.Equal("catalog-id", installation.LaunchMetadata?["CatalogItemId"]);
        Assert.Equal("app-name", installation.LaunchMetadata?["AppName"]);
    }

    [Fact]
    public async Task Uses_app_name_fallback_but_never_title_as_provider_id()
    {
        WriteManifest("fallback.item", new
        {
            DisplayName = "Unique Game Title",
            InstallLocation = _installPath,
            AppName = "stable-app-name",
            bIsIncompleteInstall = false
        });

        var result = await ScanAsync();
        var installation = Assert.Single(result.Installations);

        Assert.Equal("stable-app-name", installation.ExternalId);
        Assert.NotEqual(installation.Title, installation.ExternalId);
    }

    [Fact]
    public async Task Invalid_json_isolated_from_valid_manifest()
    {
        File.WriteAllText(Path.Combine(_root, "Manifests", "broken.item"), "{ invalid");
        WriteManifest("valid.item", new
        {
            DisplayName = "Valid Game",
            InstallLocation = _installPath,
            CatalogItemId = "valid-id",
            bIsIncompleteInstall = false
        });

        var result = await ScanAsync();

        Assert.Single(result.Installations);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task Incomplete_and_stale_manifests_are_not_published()
    {
        WriteManifest("incomplete.item", new
        {
            DisplayName = "Incomplete",
            InstallLocation = _installPath,
            CatalogItemId = "incomplete-id",
            bIsIncompleteInstall = true
        });
        WriteManifest("stale.item", new
        {
            DisplayName = "Stale",
            InstallLocation = Path.Combine(_root, "Missing"),
            CatalogItemId = "stale-id",
            bIsIncompleteInstall = false
        });

        var result = await ScanAsync();

        Assert.Empty(result.Installations);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public async Task Negative_install_size_is_projected_as_unknown()
    {
        WriteManifest("negative.item", new
        {
            DisplayName = "Negative Size",
            InstallLocation = _installPath,
            InstallSize = -1,
            CatalogItemId = "negative-id",
            bIsIncompleteInstall = false
        });

        var installation = Assert.Single((await ScanAsync()).Installations);

        Assert.Null(installation.InstalledSizeBytes);
    }

    [Fact]
    public async Task Duplicate_id_and_path_is_published_once_but_same_title_different_ids_stays_separate()
    {
        WriteManifest("first.item", new
        {
            DisplayName = "Same Title",
            InstallLocation = _installPath,
            CatalogItemId = "same-id",
            bIsIncompleteInstall = false
        });
        WriteManifest("duplicate.item", new
        {
            DisplayName = "Same Title",
            InstallLocation = _installPath,
            CatalogItemId = "same-id",
            bIsIncompleteInstall = false
        });
        WriteManifest("different.item", new
        {
            DisplayName = "Same Title",
            InstallLocation = _installPath,
            CatalogItemId = "different-id",
            bIsIncompleteInstall = false
        });

        var installations = (await ScanAsync()).Installations;

        Assert.Equal(2, installations.Count);
        Assert.Equal(["different-id", "same-id"], installations.Select(x => x.ExternalId).OrderBy(x => x).ToArray());
    }

    private async Task<SourceScanResult> ScanAsync() =>
        await new EpicLocalLibrarySource(Path.Combine(_root, "Manifests"))
            .ScanAsync(CancellationToken.None);

    private void WriteManifest(string fileName, object value) =>
        File.WriteAllText(
            Path.Combine(_root, "Manifests", fileName),
            JsonSerializer.Serialize(value));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
