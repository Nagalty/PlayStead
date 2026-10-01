using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PlayStead.Core.Persistence;
using PlayStead.Data.Catalog;
using PlayStead.Platform.Paths;
using PlayStead.UI.Bootstrap;

namespace PlayStead.UI.Tests.Bootstrap;

public sealed class CanonicalCatalogStartupTests
{
    [Fact]
    public void Public_catalog_url_is_default_and_environment_override_wins()
    {
        var previous = Environment.GetEnvironmentVariable("PLAYSTEAD_CANONICAL_CATALOG_MANIFEST_URL");
        try
        {
            Environment.SetEnvironmentVariable("PLAYSTEAD_CANONICAL_CATALOG_MANIFEST_URL", null);
            Assert.Equal(PlaySteadHost.DefaultCanonicalCatalogManifestUrl, PlaySteadHost.ResolveCanonicalCatalogManifestUri(null).ToString());

            Environment.SetEnvironmentVariable("PLAYSTEAD_CANONICAL_CATALOG_MANIFEST_URL", "https://catalog.test/custom/manifest.json");
            Assert.Equal("https://catalog.test/custom/manifest.json", PlaySteadHost.ResolveCanonicalCatalogManifestUri(Environment.GetEnvironmentVariable("PLAYSTEAD_CANONICAL_CATALOG_MANIFEST_URL")).ToString());
        }
        finally { Environment.SetEnvironmentVariable("PLAYSTEAD_CANONICAL_CATALOG_MANIFEST_URL", previous); }
    }

    [Fact]
    public async Task Production_host_initializes_catalog_without_network_dependency()
    {
        var root = Path.Combine(Path.GetTempPath(), "PlayStead.Tests", Guid.NewGuid().ToString("N"));
        var layout = UserDataLayout.FromRoot(root);
        layout.EnsureDirectoriesExist();
        using var host = PlaySteadHost.Build(layout);

        try
        {
            var options = host.Services.GetRequiredService<CatalogDatabaseOptions>();
            var store = host.Services.GetRequiredService<ICanonicalCatalogStore>();
            var state = await host.Services.GetRequiredService<LocalStartupPipeline>()
                .InitializeAsync(CancellationToken.None);

            Assert.Equal(Path.Combine(Path.GetDirectoryName(layout.DatabasePath)!, "catalog.db"), options.CatalogPath);
            Assert.Equal(Path.Combine(layout.BackupsDirectory, "Catalog"), options.BackupsDirectory);
            Assert.True(File.Exists(layout.DatabasePath));
            Assert.True(File.Exists(options.CatalogPath));
            Assert.Equal(2, (await store.GetMetadataAsync(CancellationToken.None)).SchemaVersion);
            Assert.NotNull(state.Snapshot);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
